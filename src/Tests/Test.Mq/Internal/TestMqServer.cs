using System;
using System.Threading;
using Test.Mq.Models;
using Twino.MQ;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Server;

namespace Test.Mq.Internal
{
    public class TestMqServer
    {
        public TwinoMQ Server { get; private set; }

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

        public void Initialize()
        {
            TwinoMqOptions twinoMqOptions = new TwinoMqOptions();
            twinoMqOptions.AllowedQueues = new[] {MessageA.ContentType, MessageB.ContentType, MessageC.ContentType};
            twinoMqOptions.AllowMultipleQueues = true;
            twinoMqOptions.AcknowledgeTimeout = TimeSpan.FromSeconds(90);
            twinoMqOptions.MessageTimeout = TimeSpan.FromSeconds(12);

            Server = TwinoMqBuilder.Create()
                                   .AddOptions(twinoMqOptions)
                                   .UseChannelEventHandler(new TestChannelHandler(this))
                                   .UseDeliveryHandler(async d => new TestDeliveryHandler(this))
                                   .UseClientHandler(new TestClientHandler(this))
                                   .UseAdminAuthorization<TestAdminAuthorization>()
                                   .Build();

            Channel channel = Server.CreateChannel("ch-1");
            channel.CreateQueue(MessageA.ContentType).Wait();
            channel.CreateQueue(MessageC.ContentType).Wait();

            Channel channel0 = Server.CreateChannel("ch-0");
            channel0.CreateQueue(MessageA.ContentType).Wait();

            Channel croute = Server.CreateChannel("ch-route");
            croute.Options.Status = QueueStatus.Broadcast;
            croute.CreateQueue(MessageA.ContentType).Wait();

            Channel cpush1 = Server.CreateChannel("ch-push");
            cpush1.Options.Status = QueueStatus.Push;
            cpush1.CreateQueue(MessageA.ContentType).Wait();

            Channel cpush2 = Server.CreateChannel("ch-push-cc");
            cpush2.Options.Status = QueueStatus.Push;
            cpush2.CreateQueue(MessageA.ContentType).Wait();

            Channel cpull = Server.CreateChannel("ch-pull");
            cpull.Options.Status = QueueStatus.Pull;
            cpull.CreateQueue(MessageA.ContentType).Wait();

            Channel cround = Server.CreateChannel("ch-round");
            cround.Options.Status = QueueStatus.RoundRobin;
            cround.CreateQueue(MessageA.ContentType).Wait();
        }

        public int Start(int pingInterval = 3, int requestTimeout = 4)
        {
            Random rnd = new Random();

            for (int i = 0; i < 50; i++)
            {
                try
                {
                    int port = rnd.Next(5000, 65000);
                    ServerOptions serverOptions = ServerOptions.CreateDefault();
                    serverOptions.Hosts[0].Port = port;
                    serverOptions.PingInterval = pingInterval;
                    serverOptions.RequestTimeout = requestTimeout;

                    TwinoServer server = new TwinoServer(serverOptions);
                    server.UseTwinoMQ(Server);
                    server.Start();
                    Port = port;
                    return port;
                }
                catch
                {
                    Thread.Sleep(2);
                }
            }

            return 0;
        }
    }
}