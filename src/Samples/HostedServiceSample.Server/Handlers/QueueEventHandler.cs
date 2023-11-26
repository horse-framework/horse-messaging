using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Microsoft.Extensions.Logging;

namespace HostedServiceSample.Server.Handlers;

internal class QueueEventHandler : IQueueEventHandler
{
    private readonly ILogger<QueueEventHandler> _logger;

    public QueueEventHandler(ILogger<QueueEventHandler> logger)
    {
        _logger = logger;
    }

    public Task OnCreated(HorseQueue queue)
    {
        _logger.LogInformation("[QUEUE CREATED] {Name}", queue.Name);
        return Task.CompletedTask;
    }

    public Task OnRemoved(HorseQueue queue)
    {
        _logger.LogInformation("[QUEUE REMOVED] {Name}", queue.Name);
        return Task.CompletedTask;
    }

    public Task OnConsumerSubscribed(QueueClient client)
    {
        _logger.LogInformation("[CONSUMER SUBSCRIBED] {Name} {Type}", client.Queue.Name, client.Client.Type);
        return Task.CompletedTask;
    }

    public Task OnConsumerUnsubscribed(QueueClient client)
    {
        _logger.LogInformation("[CONSUMER UNSUBSCRIBED] {Name} {Type}", client.Queue.Name, client.Client.Type);
        return Task.CompletedTask;
    }

    public Task OnStatusChanged(HorseQueue queue, QueueStatus from, QueueStatus to)
    {
        _logger.LogInformation("[QUEUE STATUS CHANGED][{Name}] {From} TO {To}", queue.Name, from, to);
        return Task.CompletedTask;
    }
}