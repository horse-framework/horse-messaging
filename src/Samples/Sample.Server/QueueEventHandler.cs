using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Sample.Server;

public class QueueEventHandler : IQueueEventHandler
{
    public Task OnCreated(HorseQueue queue)
    {
        Console.WriteLine("Queue created: " + queue.Name);
        return Task.CompletedTask;
    }

    public Task OnRemoved(HorseQueue queue)
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

    public Task OnStatusChanged(HorseQueue queue, QueueStatus @from, QueueStatus to)
    {
        Console.WriteLine($"{queue.Name} status changed from {@from} to {to}");
        return Task.CompletedTask;
    }
}