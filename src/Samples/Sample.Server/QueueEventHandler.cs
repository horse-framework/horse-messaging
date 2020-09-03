using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Sample.Server
{
    public class QueueEventHandler : IQueueEventHandler
    {
        public Task OnCreated(TwinoQueue queue)
        {
            Console.WriteLine("Queue created: " + queue.Name);
            return Task.CompletedTask;
        }

        public Task OnRemoved(TwinoQueue queue)
        {
            Console.WriteLine("Queue removed: " + queue.Name);
            return Task.CompletedTask;
        }

        public Task OnConsumerSubscribed(QueueClient client)
        {
            Console.WriteLine($"Consumer subscribed to {client.Queue.Name}");
            return Task.CompletedTask;
        }

        public Task OnConsumerUnsubscribed(QueueClient client)
        {
            Console.WriteLine($"Consumer unsubscribed from {client.Queue.Name}");
            return Task.CompletedTask;
        }

        public Task OnStatusChanged(TwinoQueue queue, QueueStatus @from, QueueStatus to)
        {
            Console.WriteLine($"{queue.Name} status changed from {@from} to {to}");
            return Task.CompletedTask;
        }
    }
}