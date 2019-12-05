using System;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Test.Mq.Internal
{
    internal class TestMqServer
    {
        public MqServer Server { get; private set; }

        public int OnQueueCreated { get; set; }
        public int OnQueueRemoved { get; set; }
        public int ClientJoined { get; set; }
        public int ClientLeft { get; set; }
        public int OnChannelStatusChanged { get; set; }
        public int OnQueueStatusChanged { get; set; }

        public int OnReceived { get; set; }
        public int OnSendStarting { get; set; }
        public int OnBeforeSend { get; set; }
        public int OnAfterSend { get; set; }
        public int OnSendCompleted { get; set; }
        public int OnAcknowledge { get; set; }
        public int OnTimeUp { get; set; }
        public int OnAcknowledgeTimeUp { get; set; }
        public int OnRemove { get; set; }
        public int OnException { get; set; }
        public int SaveMessage { get; set; }
        
        public int ClientConnected { get; set; }
        public int ClientDisconnected { get; set; }

        internal void Initialize(int port)
        {
            ServerOptions serverOptions = ServerOptions.CreateDefault();
            serverOptions.Hosts[0].Port = port;
            serverOptions.PingInterval = 3;
            serverOptions.RequestTimeout = 4;

            MqServerOptions mqOptions = new MqServerOptions();

            Server = new MqServer(serverOptions, mqOptions);
            Server.SetDefaultChannelHandler(new TestChannelHandler(this), null);
            Server.SetDefaultDeliveryHandler(new TestDeliveryHandler(this));
            Server.ClientHandler = new TestClientHandler(this);
        }

        public void Start()
        {
            Server.Start();
        }
    }
}