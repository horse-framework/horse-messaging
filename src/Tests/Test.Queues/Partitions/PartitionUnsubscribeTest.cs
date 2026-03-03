using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for unsubscribe behavior in partitioned queues.
/// Covers gaps: consumer leave → message buffering, partial unsubscribe, unsubscribe-all.
/// </summary>
public class PartitionUnsubscribeTest
{
    private static async Task<(int port, HorseQueue queue)> CreatePartitionedQueue(
        string queueName = "unsub-q",
        int maxPartitions = 5,
        int subscribersPerPartition = 2)
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create(queueName, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.None;
            opts.CommitWhen = CommitWhen.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = rider.Queue.Find(queueName);
        return (port, queue);
    }

    [Fact]
    public async Task Unsubscribe_OneOfTwo_RemainingStillReceives()
    {
        var (port, queue) = await CreatePartitionedQueue(subscribersPerPartition: 2);

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        await c2.ConnectAsync("horse://localhost:" + port);

        await c1.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });
        await c2.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });
        await Task.Delay(200);

        // c1 disconnects (leave partition)
        c1.Disconnect();
        await Task.Delay(200);

        // c2 should still be in partition
        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "w1");
        Assert.NotNull(entry);

        int received = 0;
        c2.MessageReceived += (_, _) => received++;

        // Push message to the partition
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "unsub-q");
        msg.SetStringContent("for-remaining");
        msg.AddHeader(HorseHeaders.PARTITION_LABEL, "w1");
        await producer.SendAsync(msg);

        for (int i = 0; i < 30 && received == 0; i++)
            await Task.Delay(100);

        Assert.True(received >= 1);

        producer.Disconnect();
        c2.Disconnect();
    }

    [Fact]
    public async Task Unsubscribe_AllConsumers_MessagesBuffered()
    {
        var (port, queue) = await CreatePartitionedQueue(subscribersPerPartition: 1);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync("horse://localhost:" + port);

        await consumer.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w2") });
        await Task.Delay(200);

        // Disconnect (removes from partition sub-queue)
        consumer.Disconnect();
        await Task.Delay(500);

        // Push message — no active consumer, should be stored in partition sub-queue
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "unsub-q");
        msg.SetStringContent("buffered");
        msg.AddHeader(HorseHeaders.PARTITION_LABEL, "w2");
        await producer.SendAsync(msg);
        await Task.Delay(500);

        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "w2");
        Assert.NotNull(entry);
        // Message should be in partition sub-queue store (no consumer → stored)
        Assert.False(entry.Queue.IsEmpty);

        producer.Disconnect();
    }

    [Fact]
    public async Task UnsubscribeAll_LeavesAllPartitions()
    {
        var (port, queue) = await CreatePartitionedQueue(subscribersPerPartition: 1);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync("horse://localhost:" + port);

        await consumer.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "a") });
        await Task.Delay(100);

        // Unsubscribe from all queues
        HorseResult result = await consumer.Queue.UnsubscribeFromAllQueues();
        Assert.Equal(HorseResultCode.Ok, result.Code);

        consumer.Disconnect();
    }
}
