using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Managers;

namespace HostedServiceSample.Server.Handlers;

public class SampleMemoryQueueManager: MemoryQueueManager
{
    public SampleMemoryQueueManager(HorseQueue queue): base(queue)
    {
        DeliveryHandler = new SampleMemoryDeliveryHandler(this);
    }
}

public class SampleMemoryDeliveryHandler: MemoryDeliveryHandler
{
    public SampleMemoryDeliveryHandler(IHorseQueueManager manager): base(manager) { }

    public override Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
    {
        string userIdParam = message.Message.FindHeader("UserId");
        userIdParam = string.IsNullOrEmpty(userIdParam) ? "0" : userIdParam;
        int messageOwner = int.Parse(userIdParam);
        bool hasDebugClient = messageOwner > 0 && queue.Clients.Any(m => int.Parse(m.Client.Name) == messageOwner);
        if (hasDebugClient) return Task.FromResult(receiver.Name == messageOwner.ToString());
        return Task.FromResult(receiver.Name == "0");
    }
}