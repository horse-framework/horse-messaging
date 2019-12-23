using System;
using Test.Mq.Models;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Server;

namespace Test.Mq.Internal
{
    public class TestMqServer
    {
        public MqServer Server { get; private set; }

        public int OnQueueCreated { get; set; }
        public int OnQueueRemoved { get; set; }
        public int ClientJoined { get; set; }
        public int ClientLeft { get; set; }
        public int OnChannelStatusChanged { get; set; }
        public int OnQueueStatusChanged { get; set; }
        public int OnChannelRemoved { get; set; }

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

        public int Port { get; private set; }
        
        public bool SendAcknowledgeFromMQ { get; set; }

        public void Initialize(int port)
        {
            Port = port;

            MqServerOptions mqOptions = new MqServerOptions();
            mqOptions.AllowedContentTypes = new[] {MessageA.ContentType, MessageB.ContentType, MessageC.ContentType};
            mqOptions.AllowMultipleQueues = true;
            mqOptions.AcknowledgeTimeout = TimeSpan.FromSeconds(90);

            Server = new MqServer(mqOptions);
            Server.SetDefaultChannelHandler(new TestChannelHandler(this), null);
            Server.SetDefaultDeliveryHandler(new TestDeliveryHandler(this));
            Server.ClientHandler = new TestClientHandler(this);

            Channel channel = Server.CreateChannel("ch-1");
            channel.CreateQueue(MessageA.ContentType).Wait();
            channel.CreateQueue(MessageC.ContentType).Wait();

            Channel channel0 = Server.CreateChannel("ch-0");
            channel0.CreateQueue(MessageA.ContentType).Wait();
            
            Channel croute = Server.CreateChannel("ch-route");
            croute.Options.Status = QueueStatus.Route;
            croute.CreateQueue(MessageA.ContentType).Wait();
            
            Channel cpush = Server.CreateChannel("ch-push");
            cpush.Options.Status = QueueStatus.Push;
            cpush.CreateQueue(MessageA.ContentType).Wait();
            
            Channel cpull = Server.CreateChannel("ch-pull");
            cpull.Options.Status = QueueStatus.Pull;
            cpull.CreateQueue(MessageA.ContentType).Wait();
            
            Channel cround = Server.CreateChannel("ch-round");
            cround.Options.Status = QueueStatus.RoundRobin;
            cround.CreateQueue(MessageA.ContentType).Wait();
        }

        public void Start(int pingInterval = 3, int requestTimeout = 4)
        {
            ServerOptions serverOptions = ServerOptions.CreateDefault();
            serverOptions.Hosts[0].Port = Port;
            serverOptions.PingInterval = pingInterval;
            serverOptions.RequestTimeout = requestTimeout;

            TwinoServer server = new TwinoServer(serverOptions);
            server.UseMqServer(Server);
            server.Start();
        }
    }
}