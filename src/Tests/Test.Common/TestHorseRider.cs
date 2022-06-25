using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Test.Common.Handlers;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Test.Common
{
    public class TestHorseRider
    {
        public HorseRider Rider { get; private set; }

        public int OnQueueCreated { get; set; }
        public int OnQueueRemoved { get; set; }
        public int OnSubscribed { get; set; }
        public int OnUnsubscribed { get; set; }
        public int OnQueueStatusChanged { get; set; }

        public int OnReceived { get; set; }
        public int OnSendStarting { get; set; }
        public int OnBeforeSend { get; set; }
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

        public PutBackDecision PutBack { get; set; }

        public HorseServer Server { get; private set; }

        public async Task Initialize()
        {
            Rider = HorseRiderBuilder.Create()
                .ConfigureOptions(o =>
                {
                    Random rnd = new Random();
                    o.DataPath = $"data-{Environment.TickCount}-{rnd.Next(0, 10000)}";
                })
                .ConfigureQueues(q =>
                {
                    q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(90);
                    q.Options.MessageTimeout = TimeSpan.FromSeconds(12);
                    q.Options.Type = QueueType.Push;
                    q.Options.AutoQueueCreation = true;

                    q.EventHandlers.Add(new TestQueueHandler(this));
                    
                    q.UseCustomQueueManager("Default", async m =>
                    {
                        m.Queue.Options.CommitWhen = CommitWhen.AfterReceived;
                        m.Queue.Options.PutBack = PutBackDecision.No;
                        return new TestQueueManager(this, m.Queue);
                    });
                })
                .ConfigureClients(c =>
                {
                    c.Handlers.Add(new TestClientHandler(this));
                    c.AdminAuthorizations.Add(new TestAdminAuthorization());
                })
                .Build();

            await Rider.Queue.Create("push-a", o => o.Type = QueueType.Push);
            await Rider.Queue.Create("push-a-cc", o => o.Type = QueueType.Push);
            await Rider.Queue.Create("rr-a", o => o.Type = QueueType.RoundRobin);
            await Rider.Queue.Create("pull-a", o => o.Type = QueueType.Pull);
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

                    Server = new HorseServer(serverOptions);
                    Server.UseRider(Rider);
                    Server.Start();
                    Port = port;
                    return port;
                }
                catch
                {
                    Thread.Sleep(2);
                }
            }

            Task.Run(async () =>
            {
                await Task.Delay(30000);
                Stop();
            });

            return 0;
        }

        public void Stop()
        {
            Server.Stop();
        }
    }
}