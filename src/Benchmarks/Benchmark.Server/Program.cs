using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Benchmark.Server
{
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
        private static long Count;

        private static Thread CountThread()
        {
            int seconds = 0;
            Thread thread = new Thread(async () =>
            {
                while (true)
                {
                    long prevPush = Count;
                    await Task.Delay(1000);
                    
                    long curPush = Count;
                    long difPush = curPush - prevPush;

                    seconds++;
                    
                    Console.WriteLine($"{difPush} m/s \t {curPush} total \t {seconds} secs");
                }
            });
            thread.Start();
            return thread;
        }

        static void Main(string[] args)
        {
            HorseRider rider = HorseRiderBuilder.Create()
               .ConfigureQueues(cfg =>
                {
                    cfg.EventHandlers.Add(new QEH());
                    cfg.Options.Type = QueueType.Push;
                    cfg.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No);
                })
               .Build();

            HorseServer server = new HorseServer();
            server.UseRider(rider);
            server.Run(27001);
        }
    }
}