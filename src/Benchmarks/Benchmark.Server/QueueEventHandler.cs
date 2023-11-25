using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Benchmark.Server;

public class QueueEventHandler : IQueueEventHandler
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