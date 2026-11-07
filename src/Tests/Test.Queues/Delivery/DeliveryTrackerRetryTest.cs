using System;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Xunit;

namespace Test.Queues.Delivery;

public class DeliveryTrackerRetryTest
{
    [Fact]
    public async Task TimeoutPutBack_RetryCanBeTrackedAndAcknowledgedBeforeNextScan()
    {
        HorseQueue queue = CreateQueue(QueueType.RoundRobin);
        try
        {
            var tracker = queue.Manager.DeliveryHandler.Tracker;
            QueueMessage message = CreateMessage(queue, "retry-id");
            var original = new MessageDelivery(message, null, DateTime.UtcNow.AddSeconds(-1));
            Assert.True(await tracker.Track(original));

            await ProcessTimeouts(tracker);

            Assert.Equal(DeliveryAcknowledge.Timeout, original.Acknowledge);
            Assert.Equal(0, tracker.GetDeliveryCount());
            Assert.Same(message, queue.Manager.MessageStore.ConsumeFirst());

            var retry = new MessageDelivery(message, null, DateTime.UtcNow.AddMinutes(1));
            Assert.True(await tracker.Track(retry));
            Assert.Same(retry, tracker.FindDelivery(null, "retry-id", false));

            await queue.AcknowledgeDelivered(null, message.Message.CreateAcknowledge());

            Assert.Equal(DeliveryAcknowledge.Acknowledge, retry.Acknowledge);
            Assert.Equal(0, tracker.GetDeliveryCount());
            Assert.True(message.IsRemoved);
            Assert.True(queue.IsEmpty);
            Assert.Equal(1, queue.Info.Acknowledges);
            Assert.Equal(1, queue.Info.Unacknowledges);
        }
        finally
        {
            await queue.Destroy();
        }
    }

    [Fact]
    public async Task RemovingOldDelivery_DoesNotRemoveSameIdRetry()
    {
        HorseQueue queue = CreateQueue(QueueType.RoundRobin);
        try
        {
            var tracker = queue.Manager.DeliveryHandler.Tracker;
            QueueMessage message = CreateMessage(queue, "same-id");
            var original = new MessageDelivery(message, null, DateTime.UtcNow.AddMinutes(1));
            var retry = new MessageDelivery(message, null, DateTime.UtcNow.AddMinutes(1));
            Assert.True(await tracker.Track(original));
            tracker.RemoveDelivery(original);
            Assert.True(await tracker.Track(retry));

            tracker.RemoveDelivery(original);

            Assert.Equal(1, tracker.GetDeliveryCount());
            Assert.Same(retry, tracker.FindDelivery(null, "same-id", false));
        }
        finally
        {
            await queue.Destroy();
        }
    }

    [Fact]
    public async Task PushBroadcast_MultipleReceiversShareTrackedQueueMessage()
    {
        HorseQueue queue = CreateQueue(QueueType.Push);
        try
        {
            var tracker = queue.Manager.DeliveryHandler.Tracker;
            QueueMessage message = CreateMessage(queue, "broadcast-id");
            var first = new MessageDelivery(message, null, DateTime.UtcNow.AddMinutes(1));
            var second = new MessageDelivery(message, null, DateTime.UtcNow.AddMinutes(1));

            Assert.True(await tracker.Track(first));
            Assert.True(await tracker.Track(second));
            Assert.Equal(1, tracker.GetDeliveryCount());
            Assert.Same(first, tracker.FindDelivery(null, "broadcast-id", false));

            tracker.RemoveDelivery(second);
            Assert.Same(first, tracker.FindDelivery(null, "broadcast-id", false));
        }
        finally
        {
            await queue.Destroy();
        }
    }

    private static HorseQueue CreateQueue(QueueType type)
    {
        var rider = new HorseRiderBuilder().Rider;
        rider.Queue.OptionsConfigurator = null;
        var queue = new HorseQueue(rider, "tracker-retry", new QueueOptions
        {
            Type = type,
            Acknowledge = QueueAckDecision.WaitForAcknowledge,
            PutBack = PutBackDecision.Regular,
            PutBackDelay = 0
        });
        queue.Manager = new MemoryQueueManager(queue);
        typeof(HorseQueue).GetProperty(nameof(HorseQueue.Status))!.SetValue(queue, QueueStatus.Running);
        return queue;
    }

    private static QueueMessage CreateMessage(HorseQueue queue, string id)
    {
        var message = new HorseMessage(MessageType.QueueMessage, queue.Name) { WaitResponse = true };
        message.SetMessageId(id);
        return new QueueMessage(message);
    }

    private static Task ProcessTimeouts(IDeliveryTracker tracker)
    {
        return (Task) typeof(DefaultDeliveryTracker)
            .GetMethod("ProcessDeliveries", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(tracker, null)!;
    }
}
