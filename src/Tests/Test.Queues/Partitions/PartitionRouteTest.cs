using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
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
/// Tests for message routing (push) in partitioned queues.
/// Uses PartitionTestServer for delivery tests to ensure clean CommitWhen.None semantics.
/// </summary>
public class PartitionRouteTest
{
    private static async Task<(PartitionTestContext ctx, HorseQueue queue)> CreateServer(
        string mode,
        string name = "route-q",
        int maxPartitions = 10,
        int subscribersPerPartition = 1)
    {
        var ctx = await PartitionTestServer.Create(mode);

        await ctx.Rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = ctx.Rider.Queue.Find(name);
        return (ctx, queue);
    }

    // ── Label → partition routing ─────────────────────────────────────────────

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithMatchingLabel_MessageGoesToLabelPartition(string mode)
    {
        var (ctx, queue) = await CreateServer(mode);
        await using var _ = ctx;

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(300);

        await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(600);
        Assert.Equal(1, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithLabel_MessageHasPartitionIdHeader(string mode)
    {
        var (ctx, queue) = await CreateServer(mode);
        await using var _ = ctx;

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        string receivedPartitionId = null;
        worker.MessageReceived += (_, msg) =>
        {
            receivedPartitionId = msg.FindHeader(HorseHeaders.PARTITION_ID);
        };

        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "px") });

        await Task.Delay(300);

        await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "px") });

        await Task.Delay(600);
        Assert.NotNull(receivedPartitionId);
        Assert.NotEmpty(receivedPartitionId);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_LabelStrippedFromMessageBeforeConsumerReceives(string mode)
    {
        var (ctx, queue) = await CreateServer(mode);
        await using var _ = ctx;

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        string labelHeader = "NOT_CHECKED";
        worker.MessageReceived += (_, msg) =>
        {
            labelHeader = msg.FindHeader(HorseHeaders.PARTITION_LABEL);
        };

        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "strip-test") });
        await Task.Delay(300);

        await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "strip-test") });

        await Task.Delay(600);
        Assert.Null(labelHeader);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_TwoWorkersWithDifferentLabels_EachReceivesOwnMessages(string mode)
    {
        var (ctx, queue) = await CreateServer(mode);
        await using var _ = ctx;

        HorseClient w1 = new HorseClient();
        HorseClient w2 = new HorseClient();
        HorseClient producer = new HorseClient();
        w1.AutoAcknowledge = true;
        w2.AutoAcknowledge = true;
        await w1.ConnectAsync("horse://localhost:" + ctx.Port);
        await w2.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        int receivedW1 = 0, receivedW2 = 0;
        w1.MessageReceived += (_, _) => Interlocked.Increment(ref receivedW1);
        w2.MessageReceived += (_, _) => Interlocked.Increment(ref receivedW2);

        await w1.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wA") });
        await w2.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wB") });

        await Task.Delay(400);

        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("msg"), false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wA") });

        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("msg"), false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wB") });

        await Task.Delay(1500);

        Assert.True(receivedW1 >= 2, $"w1 expected >= 2, actual {receivedW1}");
        Assert.True(receivedW2 >= 2, $"w2 expected >= 2, actual {receivedW2}");
    }

    // ── Label-less round-robin routing ────────────────────────────────────────

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_NoLabel_RoutesRoundRobinAcrossPartitions(string mode)
    {
        var (ctx, queue) = await CreateServer(mode);
        await using var _ = ctx;

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        // No-label subscribe creates a partition
        await worker.Queue.Subscribe("route-q", true);
        await Task.Delay(300);

        await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("rr-msg"), false);

        await Task.Delay(600);
        Assert.Equal(1, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_LabelHasNoSubscriber_MessageStoredInLabeledPartition(string mode)
    {
        var (ctx, queue) = await CreateServer(mode);
        await using var _ = ctx;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        // Push to a label that has no subscriber yet
        await producer.Queue.Push("route-q", Encoding.UTF8.GetBytes("msg"), false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "unknown-label") });

        await Task.Delay(600);

        // A new partition should have been created for "unknown-label"
        HorseQueue parent = ctx.Rider.Queue.Find("route-q");
        PartitionEntry entry = parent.PartitionManager.Partitions
            .FirstOrDefault(p => string.Equals(p.Label, "unknown-label", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);
        // Message is stored in the labeled partition (not dropped)
        Assert.Equal(1, entry.Queue.Manager.MessageStore.Count());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_NoLabel_RoundRobin_RoutesToPartitions(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        await ctx.Rider.Queue.Create("noorp-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = ctx.Rider.Queue.Find("noorp-q");

        HorseClient worker1 = new HorseClient();
        HorseClient worker2 = new HorseClient();
        worker1.AutoAcknowledge = true;
        worker2.AutoAcknowledge = true;

        int w1Count = 0, w2Count = 0;
        worker1.MessageReceived += (_, _) => Interlocked.Increment(ref w1Count);
        worker2.MessageReceived += (_, _) => Interlocked.Increment(ref w2Count);

        await worker1.ConnectAsync("horse://localhost:" + ctx.Port);
        await worker2.ConnectAsync("horse://localhost:" + ctx.Port);

        // Label-less subscribe — each worker gets its own partition
        await worker1.Queue.Subscribe("noorp-q", true);
        await worker2.Queue.Subscribe("noorp-q", true);
        await Task.Delay(500);

        // 2 partitions created, each with 1 client
        var parts = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(2, parts.Count);
        Assert.True(parts.All(p => p.Queue.Clients.Any()),
            $"Expected clients in all partitions. p0={parts[0].Queue.ClientsCount()}, p1={parts[1].Queue.ClientsCount()}");
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_NoLabel_NoSubscribers_ReturnsNoConsumers(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        await ctx.Rider.Queue.Create("noorp2-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        // No subscriber, label-less push
        HorseResult result = await producer.Queue.Push("noorp2-q", Encoding.UTF8.GetBytes("hello"), true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);
    }

    // ── MaxPartitionCount enforcement for labeled partitions ─────────────────

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_LabeledPartitions_ExceedMaxPartitionCount_FourthLabelRejected(string mode)
    {
        // MaxPartitionCount=3, push 4 different labels → 4th should fail
        var (ctx, queue) = await CreateServer(mode, "limit-q", maxPartitions: 3);
        await using var _ = ctx;

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        // Subscribe to 3 labeled partitions (fills MaxPartitionCount)
        await worker.Queue.Subscribe("limit-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "l1") });
        await Task.Delay(200);

        // Push messages with labels l1, l2, l3 → create partitions via push
        await producer.Queue.Push("limit-q", Encoding.UTF8.GetBytes("msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "l2") });
        await producer.Queue.Push("limit-q", Encoding.UTF8.GetBytes("msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "l3") });

        await Task.Delay(300);

        // 3 partitions should exist
        Assert.Equal(3, queue.PartitionManager.Partitions.Count());

        // 4th label push should fail (MaxPartitionCount reached)
        HorseResult result = await producer.Queue.Push("limit-q", Encoding.UTF8.GetBytes("msg"), true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "l4") });

        Assert.NotEqual(HorseResultCode.Ok, result.Code);

        // Still only 3 partitions
        Assert.Equal(3, queue.PartitionManager.Partitions.Count());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_LabeledPartitions_ExistingLabelStillWorksAtMax(string mode)
    {
        // MaxPartitionCount=2, push 2 different labels → push to existing label still works
        var (ctx, queue) = await CreateServer(mode, "reuse-q", maxPartitions: 2);
        await using var _ = ctx;

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + ctx.Port);
        await producer.ConnectAsync("horse://localhost:" + ctx.Port);

        await worker.Queue.Subscribe("reuse-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "a") });
        await Task.Delay(200);

        // Create 2nd partition via push
        await producer.Queue.Push("reuse-q", Encoding.UTF8.GetBytes("msg"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "b") });
        await Task.Delay(200);

        Assert.Equal(2, queue.PartitionManager.Partitions.Count());

        // Push to existing label "a" should still work (no new partition created)
        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await producer.Queue.Push("reuse-q", Encoding.UTF8.GetBytes("reuse"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "a") });

        await Task.Delay(600);

        Assert.Equal(1, received);
        Assert.Equal(2, queue.PartitionManager.Partitions.Count());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Subscribe_LabeledPartitions_ExceedMaxPartitionCount_FourthSubscribeRejected(string mode)
    {
        var (ctx, queue) = await CreateServer(mode, "sublimit-q", maxPartitions: 3);
        await using var _ = ctx;

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        HorseClient c3 = new HorseClient();
        HorseClient c4 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + ctx.Port);
        await c2.ConnectAsync("horse://localhost:" + ctx.Port);
        await c3.ConnectAsync("horse://localhost:" + ctx.Port);
        await c4.ConnectAsync("horse://localhost:" + ctx.Port);

        // Subscribe 3 different labels → fills MaxPartitionCount
        HorseResult r1 = await c1.Queue.Subscribe("sublimit-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "x1") });
        HorseResult r2 = await c2.Queue.Subscribe("sublimit-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "x2") });
        HorseResult r3 = await c3.Queue.Subscribe("sublimit-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "x3") });

        Assert.Equal(HorseResultCode.Ok, r1.Code);
        Assert.Equal(HorseResultCode.Ok, r2.Code);
        Assert.Equal(HorseResultCode.Ok, r3.Code);

        await Task.Delay(200);
        Assert.Equal(3, queue.PartitionManager.Partitions.Count());

        // 4th label subscribe → should be rejected (MaxPartitionCount reached)
        HorseResult r4 = await c4.Queue.Subscribe("sublimit-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "x4") });
        Assert.Equal(HorseResultCode.LimitExceeded, r4.Code);

        // Still only 3 partitions
        Assert.Equal(3, queue.PartitionManager.Partitions.Count());
    }
}
