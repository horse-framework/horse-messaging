using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for RoundRobin partition queues with WaitForAcknowledge,
/// verifying that multiple messages published rapidly are all consumed.
///
/// These tests were written to investigate a reported bug where only the first
/// of multiple messages was consumed. The investigation confirmed that the Horse
/// server correctly clears QueueClient.CurrentlyProcessing via
/// MessageDelivery.MarkAsAcknowledged() (MessageDelivery.cs:128-129), which
/// allows GetNextAvailableRRClient to find the consumer for subsequent messages.
///
/// IMPORTANT: Uses PartitionTestServer.Create(mode, configureOptions) to ensure
/// the Acknowledge option propagates correctly to the queue manager factory.
/// The legacy Create() method hard-codes Acknowledge=None in the factory.
/// </summary>
public class PartitionAckDeliveryBugTest
{
    // ─────────────────────────────────────────────────────────────────
    // RoundRobin + Partition + WaitForAck: CurrentlyProcessing lifecycle
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that after a consumer ACKs a message on a RoundRobin partition
    /// sub-queue, QueueClient.CurrentlyProcessing is properly cleared.
    ///
    /// The clearing happens inside MessageDelivery.MarkAsAcknowledged() which
    /// is called from AcknowledgeDelivered. This allows GetNextAvailableRRClient
    /// to find the consumer available for the next message.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_Partition_CurrentlyProcessing_ClearedAfterAck(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("rr-cp-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseMessage lastReceived = null;
        HorseClient consumer = new HorseClient { AutoAcknowledge = false };
        consumer.MessageReceived += (_, m) => lastReceived = m;

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.SubscribePartitioned("rr-cp-q", "lbl", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        await producer.Queue.Push("rr-cp-q", Encoding.UTF8.GetBytes("test-msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") },
            CancellationToken.None);

        for (int i = 0; i < 30 && lastReceived == null; i++)
            await Task.Delay(100);
        Assert.NotNull(lastReceived);

        await Task.Delay(500);

        // Verify state before ACK
        HorseQueue parentQueue = ctx.Rider.Queue.Find("rr-cp-q");
        PartitionEntry partition = parentQueue.PartitionManager.Partitions.First(p => p.Label == "lbl");
        var queueClient = partition.Queue.Clients.FirstOrDefault();
        Assert.NotNull(queueClient);

        int trackedCount = partition.Queue.Manager.DeliveryHandler.Tracker.GetDeliveryCount();
        Assert.Equal(1, trackedCount);
        Assert.NotNull(queueClient.CurrentlyProcessing);

        // Send ACK
        HorseMessage ack = lastReceived.CreateAcknowledge();
        await consumer.SendAsync(ack, CancellationToken.None);
        await Task.Delay(1000);

        // Verify state after ACK
        int trackedAfterAck = partition.Queue.Manager.DeliveryHandler.Tracker.GetDeliveryCount();
        Assert.Equal(0, trackedAfterAck);
        Assert.Null(queueClient.CurrentlyProcessing);

        producer.Disconnect();
        consumer.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────
    // RoundRobin + Partition + WaitForAck: Multiple messages with delayed ACK
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes 3 messages to a RoundRobin partition queue with WaitForAcknowledge.
    /// Consumer has a 500ms processing delay before sending ACK, simulating real
    /// workload (database writes, API calls, etc.).
    ///
    /// Verifies all 3 messages are consumed within a reasonable time.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_Partition_WaitForAck_DelayedAck_AllMessagesConsumed(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("rr-delay-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient { AutoAcknowledge = false };
        consumer.MessageReceived += (client, message) =>
        {
            Interlocked.Increment(ref receivedCount);
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                HorseMessage ack = message.CreateAcknowledge();
                await ((HorseClient)client).SendAsync(ack, CancellationToken.None);
            });
        };

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.SubscribePartitioned("rr-delay-q", "lbl", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("rr-delay-q", Encoding.UTF8.GetBytes($"msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") },
                CancellationToken.None);

        for (int i = 0; i < 80 && receivedCount < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, receivedCount);

        // Verify store is empty
        HorseQueue parentQueue = ctx.Rider.Queue.Find("rr-delay-q");
        PartitionEntry partition = parentQueue.PartitionManager.Partitions.First(p => p.Label == "lbl");
        int storeCount = partition.Queue.Manager.MessageStore.Count()
                         + partition.Queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(0, storeCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────
    // Push + Partition + WaitForAck: Comparison
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Push + Partition + WaitForAcknowledge with 500ms delay — same scenario.
    /// Push uses PushQueueState with a different ACK-wait mechanism.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_Partition_WaitForAck_DelayedAck_AllMessagesConsumed(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("push-delay-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient { AutoAcknowledge = false };
        consumer.MessageReceived += (client, message) =>
        {
            Interlocked.Increment(ref receivedCount);
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                HorseMessage ack = message.CreateAcknowledge();
                await ((HorseClient)client).SendAsync(ack, CancellationToken.None);
            });
        };

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.SubscribePartitioned("push-delay-q", "lbl", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("push-delay-q", Encoding.UTF8.GetBytes($"msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") },
                CancellationToken.None);

        for (int i = 0; i < 80 && receivedCount < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, receivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────
    // RoundRobin WITHOUT partition: Comparison
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// RoundRobin + WaitForAcknowledge WITHOUT partitions, same 500ms delay.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_NoPartition_WaitForAck_DelayedAck_AllMessagesConsumed(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode, o =>
        {
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        await ctx.Rider.Queue.Create("rr-nopart-q", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.AcknowledgeTimeout = TimeSpan.FromSeconds(30);
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient { AutoAcknowledge = false };
        consumer.MessageReceived += (client, message) =>
        {
            Interlocked.Increment(ref receivedCount);
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                HorseMessage ack = message.CreateAcknowledge();
                await ((HorseClient)client).SendAsync(ack, CancellationToken.None);
            });
        };

        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("rr-nopart-q", true, CancellationToken.None);
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("rr-nopart-q", Encoding.UTF8.GetBytes($"msg-{i}"), false,
                CancellationToken.None);

        for (int i = 0; i < 80 && receivedCount < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, receivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }
}
