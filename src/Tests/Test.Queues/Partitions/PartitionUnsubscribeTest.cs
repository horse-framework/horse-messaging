using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private static async Task<(PartitionTestContext ctx, HorseQueue queue)> CreatePartitionedQueue(
        string mode,
        string queueName = "unsub-q",
        int maxPartitions = 5,
        int subscribersPerPartition = 2)
    {
        var ctx = await PartitionTestServer.Create(mode);

        await ctx.Rider.Queue.Create(queueName, opts =>
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

        HorseQueue queue = ctx.Rider.Queue.Find(queueName);
        return (ctx, queue);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Unsubscribe_OneOfTwo_RemainingStillReceives(string mode)
    {
        var (ctx, queue) = await CreatePartitionedQueue(mode, subscribersPerPartition: 2);
        await using var _ = ctx;

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + ctx.Port);
        await c2.ConnectAsync("horse://localhost:" + ctx.Port);

        await c1.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") }, CancellationToken.None);
        await c2.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") }, CancellationToken.None);
        await Task.Delay(200);

        c1.Disconnect();
        await Task.Delay(200);

        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "w1");
        Assert.NotNull(entry);

        int received = 0;
        c2.MessageReceived += (_, _) => received++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "unsub-q");
        msg.SetStringContent("for-remaining");
        msg.AddHeader(HorseHeaders.PARTITION_LABEL, "w1");
        await producer.SendAsync(msg, CancellationToken.None);

        for (int i = 0; i < 30 && received == 0; i++)
            await Task.Delay(100);

        Assert.True(received >= 1);

        producer.Disconnect();
        c2.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Unsubscribe_AllConsumers_MessagesBuffered(string mode)
    {
        var (ctx, queue) = await CreatePartitionedQueue(mode, subscribersPerPartition: 1);
        await using var _ = ctx;

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync("horse://localhost:" + ctx.Port);

        await consumer.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w2") }, CancellationToken.None);
        await Task.Delay(200);

        consumer.Disconnect();
        await Task.Delay(500);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "unsub-q");
        msg.SetStringContent("buffered");
        msg.AddHeader(HorseHeaders.PARTITION_LABEL, "w2");
        await producer.SendAsync(msg, CancellationToken.None);
        await Task.Delay(500);

        PartitionEntry entry = queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "w2");
        Assert.NotNull(entry);
        Assert.False(entry.Queue.IsEmpty);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task UnsubscribeAll_LeavesAllPartitions(string mode)
    {
        var (ctx, queue) = await CreatePartitionedQueue(mode, subscribersPerPartition: 1);
        await using var _ = ctx;

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync("horse://localhost:" + ctx.Port);

        await consumer.Queue.Subscribe("unsub-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "a") }, CancellationToken.None);
        await Task.Delay(100);

        HorseResult result = await consumer.Queue.UnsubscribeFromAllQueues(CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        consumer.Disconnect();
    }
}
