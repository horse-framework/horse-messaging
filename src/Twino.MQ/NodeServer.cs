using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Connectors;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Network;
using Twino.MQ.Options;
using Twino.MQ.Security;
using Twino.Server;

namespace Twino.MQ
{
    /// <summary>
    /// 
    /// </summary>
    public class NodeServer
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
        public MqServer Server { get; }

        /// <summary>
        /// Remote node connectors
        /// </summary>
        internal TmqStickyConnector[] Connectors { get; private set; } = new TmqStickyConnector[0];

        /// <summary>
        /// Other Twino MQ server nodes that are sending messages to this server
        /// </summary>
        internal SafeList<MqClient> Clients { get; private set; } = new SafeList<MqClient>(16);

        /// <summary>
        /// Connection handler for node clients
        /// </summary>
        internal NodeConnectionHandler ConnectionHandler { get; set; }

        private bool _subscribed;
        private TwinoServer _nodeServer;

        #endregion

        #region Initialization

        /// <summary>
        /// 
        /// </summary>
        public NodeServer(MqServer server)
        {
            Server = server;
        }

        /// <summary>
        /// Inits node options and starts the connections
        /// </summary>
        public void Initialize()
        {
            if (Server.Options.Nodes == null || Server.Options.Nodes.Length < 1)
                return;

            Connectors = new TmqStickyConnector[Server.Options.Nodes.Length];

            for (int i = 0; i < Connectors.Length; i++)
            {
                NodeOptions options = Server.Options.Nodes[i];
                TimeSpan reconnect = TimeSpan.FromMilliseconds(options.ReconnectWait);

                TmqStickyConnector connector = options.KeepMessages
                                                   ? new TmqAbsoluteConnector(reconnect, () => CreateInstanceClient(options))
                                                   : new TmqStickyConnector(reconnect, () => CreateInstanceClient(options));

                Connectors[i] = connector;
                connector.Tag = options;

                connector.AddHost(options.Host);
            }
        }

        /// <summary>
        /// Client creation action for server instances
        /// </summary>
        private static TmqClient CreateInstanceClient(NodeOptions options)
        {
            TmqClient client = new TmqClient();
            client.SetClientName(options.Name);
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
        internal void SubscribeStartStop(TwinoServer server)
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
            foreach (TmqStickyConnector connector in Connectors)
                connector.Run();

            if (_nodeServer != null && _nodeServer.IsRunning)
            {
                _nodeServer.Stop();
                _nodeServer = null;
                await Task.Delay(500);
            }

            if (Server.Options.NodeHost == null)
                return;

            _nodeServer = new TwinoServer(new ServerOptions
            {
                Hosts = new List<HostOptions> { Server.Options.NodeHost },
                PingInterval = 15,
                RequestTimeout = 15
            });

            _nodeServer.Start();
        }

        /// <summary>
        /// Stop node server, disconnects from remote node servers and stops to listen incoming node client connections
        /// </summary>
        public void Stop()
        {
            foreach (TmqStickyConnector connector in Connectors)
                connector.Abort();

            _nodeServer.Stop();
            _nodeServer = null;
        }

        #endregion
    }
}