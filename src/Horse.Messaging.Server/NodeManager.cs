using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Security;
using Horse.Server;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Instance manager, manages clustered Horse MQ Servers.
    /// Accepts slave nodes and connects to master nodes.
    /// </summary>
    public class NodeManager
    {
        #region Properties

        /// <summary>
        /// Server authenticator implementation.
        /// If null, all servers will be rejected.
        /// </summary>
        internal INodeAuthenticator Authenticator { get; private set; }

        /// <summary>
        /// Messaging queue server of the node server
        /// </summary>
        public HorseRider Self { get; }

        /// <summary>
        /// Remote node connectors
        /// </summary>
        public OutgoingNode[] OutgoingNodes { get; private set; } = new OutgoingNode[0];

        /// <summary>
        /// Other Horse MQ server nodes that are sending messages to this server
        /// </summary>
        public SafeList<MessagingClient> IncomingNodes { get; } = new(16);

        /// <summary>
        /// Connection handler for node clients
        /// </summary>
        internal NodeConnectionHandler ConnectionHandler { get; set; }

        /// <summary>
        /// Event Manager for HorseEventType.ConnectedToRemoteNode 
        /// </summary>
        public EventManager ConnectedToRemoteNodeEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.DisconnectedFromRemoteNode 
        /// </summary>
        public EventManager DisconnectedFromRemoteNodeEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.RemoteNodeConnect 
        /// </summary>
        public EventManager RemoteNodeConnectEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.RemoteNodeDisconnect 
        /// </summary>
        public EventManager RemoteNodeDisconnectEvent { get; }

        private bool _subscribed;
        private HorseServer _nodeServer;

        #endregion

        #region Initialization

        /// <summary>
        /// 
        /// </summary>
        public NodeManager(HorseRider self)
        {
            Self = self;
            ConnectedToRemoteNodeEvent = new EventManager(self, HorseEventType.ConnectedToRemoteNode);
            DisconnectedFromRemoteNodeEvent = new EventManager(self, HorseEventType.DisconnectedFromRemoteNode);
            RemoteNodeConnectEvent = new EventManager(self, HorseEventType.RemoteNodeConnect);
            RemoteNodeDisconnectEvent = new EventManager(self, HorseEventType.RemoteNodeDisconnect);
        }

        /// <summary>
        /// Inits node options and starts the connections
        /// </summary>
        internal void Initialize()
        {
            if (Self.Options.Nodes == null || Self.Options.Nodes.Length < 1)
                return;

            OutgoingNodes = new OutgoingNode[Self.Options.Nodes.Length];

            for (int i = 0; i < OutgoingNodes.Length; i++)
            {
                NodeOptions options = Self.Options.Nodes[i];

                HorseClient client = new HorseClient();
                client.SetClientName(options.Name);
                client.SetClientType("server");
                client.ReconnectWait = TimeSpan.FromMilliseconds(options.ReconnectWait);
                client.Connected += _ => ConnectedToRemoteNodeEvent.Trigger(new EventSubject {Id = client.ClientId, Name = options.Name, Type = "server"});
                client.Disconnected += _ => DisconnectedFromRemoteNodeEvent.Trigger(new EventSubject {Id = client.ClientId, Name = options.Name, Type = "server"});

                if (!string.IsNullOrEmpty(options.Token))
                    client.SetClientToken(options.Token);

                OutgoingNode node = new OutgoingNode();
                node.Options = options;
                node.Client = client;

                OutgoingNodes[i] = node;
                client.Tag = options;
            }
        }

        /// <summary>
        /// Adds new remote master node
        /// </summary>
        public void AddRemoteNode(NodeOptions options)
        {
            OutgoingNode[] newArray = new OutgoingNode[OutgoingNodes.Length + 1];
            Array.Copy(OutgoingNodes, newArray, OutgoingNodes.Length);

            HorseClient client = new HorseClient();
            client.SetClientName(options.Name);
            client.SetClientType("server");
            client.ReconnectWait = TimeSpan.FromMilliseconds(options.ReconnectWait);

            if (!string.IsNullOrEmpty(options.Token))
                client.SetClientToken(options.Token);

            OutgoingNode node = new OutgoingNode();
            node.Options = options;
            node.Client = client;

            newArray[^1] = node;
            client.Tag = options;

            OutgoingNodes = newArray;
        }

        /// <summary>
        /// Sets node host options
        /// </summary>
        public void SetHost(HostOptions options)
        {
            Self.Options.NodeHost = options;
        }

        /// <summary>
        /// Sets node authenticator for using multiple nodes
        /// </summary>
        /// <exception cref="ReadOnlyException">Thrown when node authenticator already is set</exception>
        public void SetAuthenticator(INodeAuthenticator authenticator)
        {
            if (Authenticator != null)
                throw new ReadOnlyException("Node authenticator can be set only once");

            Authenticator = authenticator;
        }

        #endregion

        #region Start - Stop

        /// <summary>
        /// Subscribes start and stop events of server
        /// </summary>
        internal void SubscribeStartStop(HorseServer server)
        {
            if (_subscribed)
                return;

            _subscribed = true;

            server.OnStarted += s => _ = Start();
            server.OnStopped += _ => Stop();

            if (server.IsRunning)
                _ = Start();
        }

        /// <summary>
        /// Starts new server, connects to remote node clients and starts to listen incoming node connections
        /// </summary>
        public async Task Start()
        {
            foreach (OutgoingNode node in OutgoingNodes)
            {
                if (node?.Client == null)
                    continue;

                _ = node.Client.ConnectAsync(node.Options.Host);
            }

            if (_nodeServer != null && _nodeServer.IsRunning)
            {
                _nodeServer.Stop();
                _nodeServer = null;
                await Task.Delay(500);
            }

            if (Self.Options.NodeHost == null)
                return;

            _nodeServer = new HorseServer(new ServerOptions
                                          {
                                              Hosts = new List<HostOptions> {Self.Options.NodeHost},
                                              PingInterval = 15,
                                              RequestTimeout = 15
                                          });

            _nodeServer.UseHorseProtocol(ConnectionHandler);
            _nodeServer.Start();
        }

        /// <summary>
        /// Stop node server, disconnects from remote node servers and stops to listen incoming node client connections
        /// </summary>
        public void Stop()
        {
            foreach (OutgoingNode node in OutgoingNodes)
            {
                if (node?.Client == null)
                    continue;

                node.Client.Disconnect();
            }

            if (_nodeServer != null)
            {
                _nodeServer.Stop();
                _nodeServer = null;
            }
        }

        #endregion

        /// <summary>
        /// Sends a message to connected master nodes
        /// </summary>
        public void SendMessageToNodes(HorseMessage message)
        {
            foreach (OutgoingNode node in OutgoingNodes)
                if (node?.Client != null)
                    _ = node.Client.SendAsync(message);
        }
    }
}