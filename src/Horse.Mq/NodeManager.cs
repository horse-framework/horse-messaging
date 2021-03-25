using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Helpers;
using Horse.Mq.Network;
using Horse.Mq.Options;
using Horse.Mq.Security;
using Horse.Mq.Client;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;
using Horse.Server;

namespace Horse.Mq
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
        public HorseMq Server { get; }

        /// <summary>
        /// Remote node connectors
        /// </summary>
        public OutgoingNode[] OutgoingNodes { get; private set; } = new OutgoingNode[0];

        /// <summary>
        /// Other Horse MQ server nodes that are sending messages to this server
        /// </summary>
        internal SafeList<MqClient> Clients { get; } = new(16);

        /// <summary>
        /// Connection handler for node clients
        /// </summary>
        internal NodeConnectionHandler ConnectionHandler { get; set; }

        private bool _subscribed;
        private HorseServer _nodeServer;

        #endregion

        #region Initialization

        /// <summary>
        /// 
        /// </summary>
        public NodeManager(HorseMq server)
        {
            Server = server;
        }

        /// <summary>
        /// Inits node options and starts the connections
        /// </summary>
        internal void Initialize()
        {
            if (Server.Options.Nodes == null || Server.Options.Nodes.Length < 1)
                return;

            OutgoingNodes = new OutgoingNode[Server.Options.Nodes.Length];

            for (int i = 0; i < OutgoingNodes.Length; i++)
            {
                NodeOptions options = Server.Options.Nodes[i];
                TimeSpan reconnect = TimeSpan.FromMilliseconds(options.ReconnectWait);

                HmqStickyConnector connector = options.KeepMessages
                                                   ? new HmqAbsoluteConnector(reconnect, () => CreateInstanceClient(options))
                                                   : new HmqStickyConnector(reconnect, () => CreateInstanceClient(options));

                OutgoingNode node = new OutgoingNode();
                node.Options = options;
                node.Connector = connector;

                OutgoingNodes[i] = node;
                connector.Tag = options;

                connector.AddHost(options.Host);
            }
        }

        /// <summary>
        /// Adds new remote master node
        /// </summary>
        public void AddRemoteNode(NodeOptions options)
        {
            OutgoingNode[] newArray = new OutgoingNode[OutgoingNodes.Length + 1];
            Array.Copy(OutgoingNodes, newArray, OutgoingNodes.Length);

            TimeSpan reconnect = TimeSpan.FromMilliseconds(options.ReconnectWait);

            HmqStickyConnector connector = options.KeepMessages
                                               ? new HmqAbsoluteConnector(reconnect, () => CreateInstanceClient(options))
                                               : new HmqStickyConnector(reconnect, () => CreateInstanceClient(options));

            OutgoingNode node = new OutgoingNode();
            node.Options = options;
            node.Connector = connector;

            newArray[^1] = node;
            connector.Tag = options;

            connector.AddHost(options.Host);
            OutgoingNodes = newArray;
        }

        /// <summary>
        /// Sets node host options
        /// </summary>
        public void SetHost(HostOptions options)
        {
            Server.Options.NodeHost = options;
        }

        /// <summary>
        /// Client creation action for server instances
        /// </summary>
        private static HorseClient CreateInstanceClient(NodeOptions options)
        {
            HorseClient client = new HorseClient();
            client.SetClientName(options.Name);

            if (!string.IsNullOrEmpty(options.Token))
                client.SetClientToken(options.Token);

            client.SetClientType("server");
            return client;
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
                if (node?.Connector == null)
                    continue;

                if (!node.Connector.IsRunning)
                    node.Connector.Run();
            }

            if (_nodeServer != null && _nodeServer.IsRunning)
            {
                _nodeServer.Stop();
                _nodeServer = null;
                await Task.Delay(500);
            }

            if (Server.Options.NodeHost == null)
                return;

            _nodeServer = new HorseServer(new ServerOptions
                                          {
                                              Hosts = new List<HostOptions> {Server.Options.NodeHost},
                                              PingInterval = 15,
                                              RequestTimeout = 15
                                          });

            _nodeServer.UseHmq(ConnectionHandler);
            _nodeServer.Start();
        }

        /// <summary>
        /// Stop node server, disconnects from remote node servers and stops to listen incoming node client connections
        /// </summary>
        public void Stop()
        {
            foreach (OutgoingNode node in OutgoingNodes)
            {
                if (node?.Connector == null)
                    continue;

                node.Connector.Abort();
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
                if (node != null && node.Connector != null)
                    _ = node.Connector.GetClient()?.SendAsync(message);
        }
    }
}