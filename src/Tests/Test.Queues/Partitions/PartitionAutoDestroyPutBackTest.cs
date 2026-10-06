using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Partitions;

public class PartitionAutoDestroyPutBackTest
{
    [Theory]
    [InlineData("memory", false)]
    [InlineData("persistent", false)]
    [InlineData("memory", true)]
    [InlineData("persistent", true)]
    public async Task QueueAutoDestroy_WaitsForAcknowledgeDecisionBeforeDelayedPutBack(string mode, bool timeout)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });
        HorseQueue queue = await ctx.Rider.Queue.Create("nack-putback-handoff", o =>
        {
            o.Type = QueueType.Pull;
            o.AutoDestroy = QueueDestroy.NoMessages;
            o.PutBack = PutBackDecision.Regular;
            o.PutBackDelay = 1000;
        });
        DelayedAcknowledgeHandler handler = new(queue.Manager.DeliveryHandler, timeout);
        queue.Manager.GetType().GetProperty(nameof(IHorseQueueManager.DeliveryHandler)).SetValue(queue.Manager, handler);
        HorseMessage message = new(MessageType.QueueMessage, queue.Name) { WaitResponse = true };
        message.SetMessageId("nack-handoff");
        MessageDelivery delivery = new(new QueueMessage(message), null,
            timeout ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddSeconds(30));
        Assert.True(await handler.Tracker.Track(delivery));
        HorseMessage nack = message.CreateAcknowledge();
        nack.AddHeader(HorseHeaders.NEGATIVE_ACKNOWLEDGE_REASON, "retry");

        Task processing = timeout ? Task.CompletedTask : queue.AcknowledgeDelivered(null, nack);
        try
        {
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // A late ACK removes the timed-out tracker entry while the retry decision still waits.
            if (timeout)
                await queue.AcknowledgeDelivered(null, message.CreateAcknowledge());
            Assert.True(queue.IsEmpty);
            Assert.Equal(0, handler.Tracker.GetDeliveryCount());
            Assert.Equal(0, queue.GetMessageCountPendingForPutBack());
            await queue.CheckAutoDestroy();
            Assert.False(queue.IsDestroyed);
        }
        finally
        {
            handler.Release.TrySetResult(true);
            await processing;
        }

        Assert.True(await WaitUntil(() => queue.GetMessageCountPendingForPutBack() == 1));
        await queue.CheckAutoDestroy();
        Assert.False(queue.IsDestroyed);
        Assert.True(await WaitUntil(() => !queue.IsEmpty));
        Assert.NotNull(queue.Manager.MessageStore.Find(message.MessageId));
        queue.ClearMessages();
        await queue.CheckAutoDestroy();
        Assert.True(queue.IsDestroyed);
    }

    [Theory]
    [InlineData("memory", QueueDestroy.NoMessages)]
    [InlineData("memory", QueueDestroy.Empty)]
    [InlineData("persistent", QueueDestroy.NoMessages)]
    [InlineData("persistent", QueueDestroy.Empty)]
    public async Task QueueAutoDestroy_WaitsForDelayedPutBack(string mode, QueueDestroy policy)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        HorseQueue queue = await ctx.Rider.Queue.Create("putback-auto-destroy", o =>
        {
            o.Type = QueueType.Pull;
            o.AutoDestroy = policy;
            o.PutBackDelay = 1000;
        });
        HorseMessage message = new(MessageType.QueueMessage, queue.Name);
        message.SetMessageId("delayed-retry");
        await queue.ApplyDecision(Decision.PutBackMessage(true), new QueueMessage(message));

        Assert.True(queue.IsEmpty);
        Assert.Equal(1, queue.GetMessageCountPendingForPutBack());
        Assert.False(queue.IsIdleForDestroy);
        await queue.CheckAutoDestroy();
        Assert.False(queue.IsDestroyed);

        Assert.True(await WaitUntil(() => !queue.IsEmpty), "delayed message was not returned to the queue");
        Assert.Equal(0, queue.GetMessageCountPendingForPutBack());
        await queue.CheckAutoDestroy();
        Assert.False(queue.IsDestroyed);

        queue.ClearMessages();
        await queue.CheckAutoDestroy();
        Assert.True(queue.IsDestroyed);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PartitionReaper_PreservesDelayedNack_AndDestroysAfterSuccessfulRetry(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });
        HorseQueue parent = await ctx.Rider.Queue.Create("partition-putback", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.Regular;
            o.PutBackDelay = 6000;
            o.Partition = new PartitionOptions
            {
                Enabled = true,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = 2
            };
        });

        using HorseClient consumer = new() { AutoAcknowledge = false };
        using HorseClient producer = new();
        TaskCompletionSource<HorseMessage> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<HorseMessage> second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int received = 0;
        consumer.MessageReceived += (_, message) =>
        {
            if (Interlocked.Increment(ref received) == 1)
                first.TrySetResult(message);
            else
                second.TrySetResult(message);
        };

        try
        {
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            Assert.Equal(HorseResultCode.Ok,
                (await consumer.Queue.SubscribePartitioned(parent.Name, "tenant", true, CancellationToken.None)).Code);
            HorseQueue partition = parent.PartitionManager.Partitions.Single().Queue;
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await producer.Queue.Push(parent.Name, "retry-me"u8.ToArray(), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant") }, CancellationToken.None);

            HorseMessage initial = await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await consumer.SendNegativeAck(initial);
            Assert.True(await WaitUntil(() => partition.GetMessageCountPendingForPutBack() == 1));
            Assert.True(partition.IsEmpty);
            Assert.Equal(0, partition.Manager.DeliveryHandler.Tracker.GetDeliveryCount());
            Assert.False(partition.IsIdleForDestroy);

            // Cross two reaper intervals while the only message lives in the putback list.
            await Task.Delay(4500);
            Assert.False(partition.IsDestroyed);
            HorseMessage retried = await second.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(initial.MessageId, retried.MessageId);
            await consumer.SendAck(retried);

            Assert.True(await WaitUntil(() => partition.IsDestroyed), "partition was not reclaimed after the retry completed");
            Assert.Equal(2, Volatile.Read(ref received));
        }
        finally
        {
            producer.Disconnect();
            consumer.Disconnect();
        }
    }

    private static async Task<bool> WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        return condition();
    }

    private sealed class DelayedAcknowledgeHandler(IQueueDeliveryHandler inner, bool delayTimeout) : IQueueDeliveryHandler
    {
        public IHorseQueueManager Manager => inner.Manager;
        public IDeliveryTracker Tracker => inner.Tracker;
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
            => inner.ReceivedFromProducer(queue, message, sender);
        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message) => inner.BeginSend(queue, message);
        public Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
            => inner.CanConsumerReceive(queue, message, receiver);
        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
            => inner.ConsumerReceiveFailed(queue, delivery, receiver);
        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message) => inner.EndSend(queue, message);
        public async Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery)
        {
            if (delayTimeout)
            {
                Entered.TrySetResult(true);
                await Release.Task;
            }
            return await inner.AcknowledgeTimeout(queue, delivery);
        }

        public async Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            Entered.TrySetResult(true);
            await Release.Task;
            return await inner.AcknowledgeReceived(queue, acknowledgeMessage, delivery, success);
        }
    }
}
