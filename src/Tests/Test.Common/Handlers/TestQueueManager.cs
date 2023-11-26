using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Managers;

namespace Test.Common.Handlers;

public class TestQueueManager : MemoryQueueManager
{
    private readonly TestHorseRider _rider;

    public TestQueueManager(TestHorseRider rider, HorseQueue queue)
        : base(queue)
    {
        _rider = rider;
        DeliveryHandler = new TestMessageDeliveryHandler(rider, this);
    }

    public override Task OnMessageTimeout(QueueMessage message)
    {
        _rider.OnTimeUp++;
        return base.OnMessageTimeout(message);
    }

    public override Task OnExceptionThrown(string hint, QueueMessage message, Exception exception)
    {
        _rider.OnException++;
        return base.OnExceptionThrown(hint, message, exception);
    }

    public override Task<bool> SaveMessage(QueueMessage message)
    {
        _rider.SaveMessage++;
        return base.SaveMessage(message);
    }

    public override Task<bool> RemoveMessage(string messageId)
    {
        _rider.OnRemove++;
        return base.RemoveMessage(messageId);
    }

    public override Task<bool> RemoveMessage(QueueMessage message)
    {
        _rider.OnRemove++;
        return base.RemoveMessage(message);
    }
}