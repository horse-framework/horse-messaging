using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace AdvancedSample.Server.Implementations.Queue
{
    public class CustomQueueEventHandler : IQueueEventHandler
    {
        public Task OnCreated(HorseQueue queue)
        {
            Worker._logger.LogInformation("Queue created: " + queue.Name);
            return Task.CompletedTask;
        }

        public Task OnRemoved(HorseQueue queue)
        {
            Console.WriteLine("Queue removed: " + queue.Name);
            return Task.CompletedTask;
        }

        public Task OnConsumerSubscribed(QueueClient client)
        {


            Worker._logger.LogInformation($"Consumer subscribed to {client.Queue.Name}");
            return Task.CompletedTask;



        }

        public Task OnConsumerUnsubscribed(QueueClient client)
        {
            Console.WriteLine($"Consumer unsubscribed from {client.Queue.Name}");
            return Task.CompletedTask;
        }

        public Task OnStatusChanged(HorseQueue queue, QueueStatus @from, QueueStatus to)
        {
            Console.WriteLine($"{queue.Name} status changed from {@from} to {to}");
            return Task.CompletedTask;
        }
    }
}
