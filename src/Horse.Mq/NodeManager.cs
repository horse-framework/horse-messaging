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
        internal HmqStickyConnector[] Connectors { get; private set; } = new HmqStickyConnector[0];

        /// <summary>
        /// Other Horse MQ server nodes that are sending messages to this server
        /// </summary>
        internal SafeList<MqClient> Clients { get; private set; } = new SafeList<MqClient>(16);

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

            Connectors = new HmqStickyConnector[Server.Options.Nodes.Length];

            for (int i = 0; i < Connectors.Length; i++)
            {
                NodeOptions options = Server.Options.Nodes[i];
                TimeSpan reconnect = TimeSpan.FromMilliseconds(options.ReconnectWait);

                HmqStickyConnector connector = options.KeepMessages
                                                   ? new HmqAbsoluteConnector(reconnect, () => CreateInstanceClient(options))
                                                   : new HmqStickyConnector(reconnect, () => CreateInstanceClient(options));

                Connectors[i] = connector;
                connector.Tag = options;

                connector.AddHost(options.Host);
            }
        }

        /// <summary>
        /// Adds new remote master node
        /// </summary>
        public void AddRemoteNode(NodeOptions options)
        {
            HmqStickyConnector[] newArray = new HmqStickyConnector[Connectors.Length + 1];
            Array.Copy(Connectors, newArray, Connectors.Length);

            TimeSpan reconnect = TimeSpan.FromMilliseconds(options.ReconnectWait);

            HmqStickyConnector connector = options.KeepMessages
                                               ? new HmqAbsoluteConnector(reconnect, () => CreateInstanceClient(options))
                                               : new HmqStickyConnector(reconnect, () => CreateInstanceClient(options));

            newArray[^1] = connector;
            connector.Tag = options;

            connector.AddHost(options.Host);
            Connectors = newArray;
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
            server.OnStopped += s => Stop();

            if (server.IsRunning)
                _ = Start();
        }

        /// <summary>
        /// Starts new server, connects to remote node clients and starts to listen incoming node connections
        /// </summary>
        public async Task Start()
        {
            foreach (HmqStickyConnector connector in Connectors)
            {
                if (!connector.IsRunning)
                    connector.Run();
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
            foreach (HmqStickyConnector connector in Connectors)
                connector.Abort();

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
            foreach (HmqStickyConnector connector in Connectors)
                _ = connector.GetClient()?.SendAsync(message);
        }
    }
}