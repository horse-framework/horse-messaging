using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// A push checks the queue status first and stores the message only after several awaits
/// (QueueLock, cluster replication ack, ReceivedFromProducer). The queue looked idle during that
/// window, so auto-destroy could remove it and the message was written into a destroyed queue:
/// lost, while the producer was told Ok.
/// </summary>
public class PushAutoDestroyRaceTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoDestroy_DoesNotRemoveQueue_WhilePushIsInFlight(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o => o.Acknowledge = QueueAckDecision.None);
        HorseQueue queue = await ctx.Rider.Queue.Create("push-race", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.AutoDestroy = QueueDestroy.NoMessages;
        });

        BlockingProducerHandler handler = new(queue.Manager.DeliveryHandler);
        queue.Manager.GetType().GetProperty(nameof(IHorseQueueManager.DeliveryHandler)).SetValue(queue.Manager, handler);

        HorseMessage message = new(MessageType.QueueMessage, queue.Name);
        message.SetStringContent("in-flight");
        Task<PushResult> push = queue.Push(message);

        try
        {
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(queue.IsEmpty);
            Assert.False(queue.IsIdleForDestroy);

            await queue.CheckAutoDestroy();
            Assert.False(queue.IsDestroyed);
            Assert.False(queue.IsClosingForDestroy);
        }
        finally
        {
            handler.Release.TrySetResult(true);
        }

        Assert.Equal(PushResult.Success, await push);
        Assert.Same(queue, ctx.Rider.Queue.Find(queue.Name));
        Assert.Equal(1, queue.Manager.MessageStore.Count());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushToClosingPartition_IsRoutedToTheRecreatedPartition(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode, o => o.Acknowledge = QueueAckDecision.None);
        HorseQueue parent = await ctx.Rider.Queue.Create("closing-partition", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.Partition = new PartitionOptions
            {
                Enabled = true,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                // The test drives destroy itself; keep the reaper out of the way.
                AutoDestroyIdleSeconds = 600
            };
        });

        Assert.Equal(PushResult.Success, await parent.Push(Labeled(parent, "first")));
        HorseQueue closing = parent.PartitionManager.Partitions.Single().Queue;
        closing.ClearMessages();

        // Auto-destroy claims the partition; a push that already picked it must not be lost.
        Assert.True(closing.TryCloseForDestroy());
        Task<PushResult> push = parent.Push(Labeled(parent, "second"));
        await Task.Delay(200);
        Assert.False(push.IsCompleted);

        await ctx.Rider.Queue.Remove(closing);

        Assert.Equal(PushResult.Success, await push.WaitAsync(TimeSpan.FromSeconds(10)));
        HorseQueue recreated = parent.PartitionManager.Partitions.Single().Queue;
        Assert.NotSame(closing, recreated);
        Assert.Equal(1, recreated.Manager.MessageStore.Count());
        Assert.Equal("second", recreated.Manager.MessageStore.ConsumeFirst().Message.GetStringContent());
    }

    private static HorseMessage Labeled(HorseQueue parent, string content)
    {
        HorseMessage message = new(MessageType.QueueMessage, parent.Name);
        message.SetStringContent(content);
        message.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant");
        return message;
    }

    private sealed class BlockingProducerHandler(IQueueDeliveryHandler inner) : IQueueDeliveryHandler
    {
        public IHorseQueueManager Manager => inner.Manager;
        public IDeliveryTracker Tracker => inner.Tracker;
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            Entered.TrySetResult(true);
            await Release.Task;
            return await inner.ReceivedFromProducer(queue, message, sender);
        }

        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message) => inner.BeginSend(queue, message);
        public Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
            => inner.CanConsumerReceive(queue, message, receiver);
        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
            => inner.ConsumerReceiveFailed(queue, delivery, receiver);
        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message) => inner.EndSend(queue, message);
        public Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery) => inner.AcknowledgeTimeout(queue, delivery);
        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
            => inner.AcknowledgeReceived(queue, acknowledgeMessage, delivery, success);
    }
}
