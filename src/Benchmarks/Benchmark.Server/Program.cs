using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Benchmark.Server
{
    public class EH : IErrorHandler
    {
        public void Error(string hint, Exception exception, string payload)
        {
            Console.WriteLine($"ERROR\t{hint} : {exception}");
        }
    }

    public class QEH : IQueueEventHandler
    {
        public Task OnCreated(HorseQueue queue)
        {
            Console.WriteLine("Queue Created: " + queue.Name);
            return Task.CompletedTask;
        }

        public Task OnRemoved(HorseQueue queue) => Task.CompletedTask;

        public Task OnConsumerSubscribed(QueueClient client) => Task.CompletedTask;
        public Task OnConsumerUnsubscribed(QueueClient client) => Task.CompletedTask;

        public Task OnStatusChanged(HorseQueue queue, QueueStatus @from, QueueStatus to) => Task.CompletedTask;
    }

    class Program
    {
        private static HorseRider _rider;
        
        private static Thread CountThread()
        {
            Thread thread = new Thread(async () =>
            {
                while (true)
                {
                    HorseQueue q = _rider?.Queue.Find("Test0");
                    if (q == null)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    long mc = q.MessageCount();
                    Console.WriteLine($"Message count: {mc}");
                    await Task.Delay(1000);
                }
            });
            thread.Start();
            return thread;
        }

        static void Main(string[] args)
        {
            _ = CountThread();
            _rider = HorseRiderBuilder.Create()
               .ConfigureQueues(cfg =>
                {
                    cfg.EventHandlers.Add(new QEH());
                    //cfg.Options.Acknowledge = QueueAckDecision.None;
                    cfg.Options.Type = QueueType.Push;
                    cfg.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
                    cfg.UseJustAllowDeliveryHandler();
                    //cfg.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No);
                })
               .ConfigureCache(cfg =>
                {
                    cfg.Options.DefaultDuration = TimeSpan.FromMinutes(30);
                })
               .AddErrorHandler<EH>()
               .Build();

            HorseServer server = new HorseServer();
            server.UseRider(_rider);
            server.Run(27001);
        }
    }
}