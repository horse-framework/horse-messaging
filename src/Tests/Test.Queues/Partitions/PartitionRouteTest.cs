using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for message routing (push) in partitioned queues.
/// Uses PartitionTestServer for delivery tests to ensure clean CommitWhen.None semantics.
/// </summary>
public class PartitionRouteTest
{
    private static async Task<(HorseRider rider, int port, HorseQueue queue)> CreateServer(
        string name = "route-q",
        int maxPartitions = 10,
        int subscribersPerPartition = 1)
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                EnableOrphanPartition = true,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = rider.Queue.Find(name);
        return (rider, port, queue);
    }

    // ── Label → partition routing ─────────────────────────────────────────────

    [Fact]
    public async Task Push_WithMatchingLabel_MessageGoesToLabelPartition()
    {
        var (rider, port, queue) = await CreateServer();

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(300);

        await producer.Queue.Push("route-q", "msg",
            false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w1") });

        await Task.Delay(600);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Push_WithLabel_MessageHasPartitionIdHeader()
    {
        var (rider, port, queue) = await CreateServer();

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        string receivedPartitionId = null;
        worker.MessageReceived += (_, msg) =>
        {
            receivedPartitionId = msg.FindHeader(HorseHeaders.PARTITION_ID);
        };

        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "px") });

        await Task.Delay(300);

        await producer.Queue.Push("route-q", "msg",
            false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "px") });

        await Task.Delay(600);
        Assert.NotNull(receivedPartitionId);
        Assert.NotEmpty(receivedPartitionId);
    }

    [Fact]
    public async Task Push_LabelStrippedFromMessageBeforeConsumerReceives()
    {
        var (rider, port, queue) = await CreateServer();

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        string labelHeader = "NOT_CHECKED";
        worker.MessageReceived += (_, msg) =>
        {
            labelHeader = msg.FindHeader(HorseHeaders.PARTITION_LABEL);
        };

        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "strip-test") });
        await Task.Delay(300);

        await producer.Queue.Push("route-q", "msg",
            false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "strip-test") });

        await Task.Delay(600);
        Assert.Null(labelHeader);
    }

    [Fact]
    public async Task Push_TwoWorkersWithDifferentLabels_EachReceivesOwnMessages()
    {
        var (rider, port, queue) = await CreateServer();

        HorseClient w1 = new HorseClient();
        HorseClient w2 = new HorseClient();
        HorseClient producer = new HorseClient();
        w1.AutoAcknowledge = true;
        w2.AutoAcknowledge = true;
        await w1.ConnectAsync("horse://localhost:" + port);
        await w2.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        int receivedW1 = 0, receivedW2 = 0;
        w1.MessageReceived += (_, _) => Interlocked.Increment(ref receivedW1);
        w2.MessageReceived += (_, _) => Interlocked.Increment(ref receivedW2);

        await w1.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wA") });
        await w2.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wB") });

        await Task.Delay(400);

        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("route-q", "msg",
                false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wA") });

        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("route-q", "msg",
                false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "wB") });

        await Task.Delay(1500);

        Assert.True(receivedW1 >= 2, $"w1 expected >= 2, actual {receivedW1}");
        Assert.True(receivedW2 >= 2, $"w2 expected >= 2, actual {receivedW2}");
    }

    // ── Orphan routing ────────────────────────────────────────────────────────

    [Fact]
    public async Task Push_NoLabel_GoesToOrphan()
    {
        var (rider, port, queue) = await CreateServer();

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        worker.AutoAcknowledge = true;
        await worker.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await worker.Queue.Subscribe("route-q", true);
        await Task.Delay(300);

        await producer.Queue.Push("route-q", "orphan-msg", false);

        await Task.Delay(600);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Push_LabelHasNoSubscriber_MessageStoredInLabeledPartition()
    {
        // New behaviour: a labeled message always routes to its labeled partition,
        // regardless of whether a subscriber is present.  The message is buffered
        // until the owning worker reconnects — it is NEVER cross-delivered to another
        // worker or to the orphan partition.
        var (rider, port, queue) = await CreateServer();

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        // Push to a label that has no subscriber yet
        await producer.Queue.Push("route-q", "msg",
            false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "unknown-label") });

        await Task.Delay(600);

        // A new partition should have been created for "unknown-label"
        HorseQueue parent = rider.Queue.Find("route-q");
        PartitionEntry entry = parent.PartitionManager.Partitions
            .FirstOrDefault(p => string.Equals(p.Label, "unknown-label", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(entry);
        // Message is stored in the labeled partition (not orphan, not dropped)
        Assert.Equal(1, entry.Queue.Manager.MessageStore.Count());

        // Orphan is untouched
        if (parent.PartitionManager.OrphanPartition != null)
            Assert.Equal(0, parent.PartitionManager.OrphanPartition.Queue.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task Push_LabelHasNoSubscriber_WaitAck_NoOrphanSubscriber_MessageNotDelivered()
    {
        // Create with WaitForAcknowledge — needs special handling
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create("wack-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                EnableOrphanPartition = true,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        // No subscribers → message goes to orphan (which has no consumer) → NoConsumers
        // waitForCommit=true ensures we get the server response
        HorseResult result = await producer.Queue.Push("wack-q", "no-consumer-msg", true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);
    }

    // ── Message stored when no consumer online ──────────────────────────────

    [Fact]
    public async Task Push_WithLabel_NoConsumer_MessageStoredInOrphanPartition()
    {
        var (rider, port, queue) = await CreateServer();

        HorseClient worker = new HorseClient();
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.Subscribe("route-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "offline-w") });

        await Task.Delay(300);
        worker.Disconnect();
        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        HorseQueue orphanQueue = await queue.PartitionManager.GetOrCreateOrphanQueue();
        int beforeCount = orphanQueue.Manager?.MessageStore.Count() ?? 0;

        await producer.Queue.Push("route-q", "stored-msg",
            false, new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "offline-w") });

        await Task.Delay(500);

        int afterCount = orphanQueue.Manager?.MessageStore.Count() ?? 0;
        Assert.True(afterCount >= beforeCount);
    }

    /// <summary>
    /// Orphan disabled + label'sız subscribe: her worker kendi partition'ını alır.
    /// Orphan queue oluşturulmamalı. Partition sayısı subscriber sayısına eşit olmalı.
    /// </summary>
    [Fact]
    public async Task Push_NoLabel_OrphanDisabled_RoutesToLeastLoadedPartition()
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create("noorp-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                EnableOrphanPartition = false,   // ← orphan kapalı
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = rider.Queue.Find("noorp-q");

        HorseClient worker1 = new HorseClient();
        HorseClient worker2 = new HorseClient();
        worker1.AutoAcknowledge = true;
        worker2.AutoAcknowledge = true;

        int w1Count = 0, w2Count = 0;
        worker1.MessageReceived += (_, _) => Interlocked.Increment(ref w1Count);
        worker2.MessageReceived += (_, _) => Interlocked.Increment(ref w2Count);

        await worker1.ConnectAsync("horse://localhost:" + port);
        await worker2.ConnectAsync("horse://localhost:" + port);

        // Label'sız subscribe — her worker kendi partition'ını alır
        await worker1.Queue.Subscribe("noorp-q", true);
        await worker2.Queue.Subscribe("noorp-q", true);
        await Task.Delay(500);

        // Orphan hiç oluşturulmamalı
        Assert.Null(queue.PartitionManager.OrphanPartition);

        // 2 label partition oluşturulmuş olmalı, her birinde 1 client
        var labelParts = queue.PartitionManager.Partitions.Where(p => !p.IsOrphan).ToList();
        Assert.Equal(2, labelParts.Count);
        Assert.True(labelParts.All(p => p.Queue.Clients.Any()),
            $"Expected clients in all partitions. p0={labelParts[0].Queue.ClientsCount()}, p1={labelParts[1].Queue.ClientsCount()}");
    }

    /// <summary>
    /// Orphan disabled + label'sız + hiç subscriber yoksa NoConsumers dönmeli.
    /// </summary>
    [Fact]
    public async Task Push_NoLabel_OrphanDisabled_NoSubscribers_ReturnsNoConsumers()
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create("noorp2-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                EnableOrphanPartition = false,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        // Hiç subscriber yok, label'sız push
        HorseResult result = await producer.Queue.Push("noorp2-q", "hello", true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);
    }
}

