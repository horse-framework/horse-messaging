using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Test.Queues.Partitions;

#region Models

[QueueName("QueueA")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.JustRequest)]
public class QueueAModel
{
    public string TenantId { get; set; }
    public int Sequence { get; set; }
}

[QueueName("QueueB")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.JustRequest)]
public class QueueBModel
{
    public string TenantId { get; set; }
    public int Sequence { get; set; }
}

[QueueName("QueueC")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.JustRequest)]
public class QueueCModel
{
    public string TenantId { get; set; }
    public int Sequence { get; set; }
}

[QueueName("QueueD")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
public class QueueDModel
{
    public string TenantId { get; set; }
    public int Sequence { get; set; }
}

#endregion

#region Per-Test Tracking Infrastructure

/// <summary>
///     Thread-safe tracker that records per-tenant consume order for each queue.
///     Each test creates its own instance — no shared static state.
///     Key = "QueueName:TenantId", Value = ordered list of sequence numbers consumed.
/// </summary>
public class ConsumeTracker
{
    private readonly ConcurrentDictionary<string, List<int>> _records = new();

    public void Record(string queueName, string tenantId, int sequence)
    {
        string key = $"{queueName}:{tenantId}";
        List<int> list = _records.GetOrAdd(key, _ => new List<int>());
        lock (list)
        {
            list.Add(sequence);
        }
    }

    public List<int> Get(string queueName, string tenantId)
    {
        string key = $"{queueName}:{tenantId}";
        return _records.TryGetValue(key, out List<int> list) ? list : new List<int>();
    }

    public int TotalCount()
    {
        int count = 0;
        foreach (KeyValuePair<string, List<int>> kv in _records)
            lock (kv.Value)
            {
                count += kv.Value.Count;
            }
        return count;
    }
}

/// <summary>
///     DI-injectable holder for per-test ConsumeTracker.
///     Registered as singleton in each worker's ServiceCollection so that
///     scoped consumers can access the test-specific tracker via constructor injection.
/// </summary>
public class TrackerAccessor(ConsumeTracker tracker)
{
    public ConsumeTracker Tracker { get; } = tracker;
}

#endregion

#region Consumers (DI-injected tracker)

[AutoAck]
public class QueueAConsumer(TrackerAccessor accessor) : IQueueConsumer<QueueAModel>
{
    public Task Consume(HorseMessage message, QueueAModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        accessor.Tracker.Record("QueueA", model.TenantId, model.Sequence);
        return Task.CompletedTask;
    }
}

[AutoAck]
public class QueueBConsumer(TrackerAccessor accessor) : IQueueConsumer<QueueBModel>
{
    public Task Consume(HorseMessage message, QueueBModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        accessor.Tracker.Record("QueueB", model.TenantId, model.Sequence);
        return Task.CompletedTask;
    }
}

[AutoAck]
public class QueueCConsumer(TrackerAccessor accessor) : IQueueConsumer<QueueCModel>
{
    public Task Consume(HorseMessage message, QueueCModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        accessor.Tracker.Record("QueueC", model.TenantId, model.Sequence);
        return Task.CompletedTask;
    }
}

[AutoAck]
public class QueueDConsumer(TrackerAccessor accessor) : IQueueConsumer<QueueDModel>
{
    public Task Consume(HorseMessage message, QueueDModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        accessor.Tracker.Record("QueueD", model.TenantId, model.Sequence);
        return Task.CompletedTask;
    }
}

#endregion

/// <summary>
///     Complex integration tests for partitioned queues.
///     Verifies multi-queue, multi-tier, multi-tenant scenarios with
///     AddScopedConsumer label overloads and queueNameTransform overloads.
///     Each scenario runs in both memory and persistent mode.
///     Each test creates its own ConsumeTracker — tests are fully isolated.
/// </summary>
public class PartitionComplexIntegrationTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // 2. Worker Reassignment After Partition Destroyed
    // ═══════════════════════════════════════════════════════════════════════

    #region Worker Reassignment

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task WorkerPool_2Workers_3Labels_WorkerReassignedAfterPartitionDestroyed(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "reassign-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 1,
            AutoDestroy = PartitionAutoDestroy.NoMessages, AutoDestroyIdleSeconds = 2
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "reassign-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "reassign-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "reassign-q", "label-a");
        await PushLabeled(producer, "reassign-q", "label-a");
        await PushLabeled(producer, "reassign-q", "label-b");
        await PushLabeled(producer, "reassign-q", "label-b");
        await PushLabeled(producer, "reassign-q", "label-c");
        await PushLabeled(producer, "reassign-q", "label-c");
        await PushLabeled(producer, "reassign-q", "label-c");

        await WaitUntil(() => queue.PartitionManager.Partitions.Count() == 3);
        await WaitUntil(() => tracker.TotalCount() >= 4);

        // After auto-destroy fires, freed worker is reassigned to label-c
        await WaitUntil(() => tracker.TotalCount() >= 7, 15_000);
        Assert.True(tracker.TotalCount() >= 7, $"Expected ≥7, got {tracker.TotalCount()}");

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 5. SubscribersPerPartition
    // ═══════════════════════════════════════════════════════════════════════

    #region SubscribersPerPartition

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SubscribersPerPartition2_OnlyTwoWorkersAssignedPerPartition(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "spp-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 2,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "spp-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "spp-q");
        HorseClient w3 = await BuildPoolWorker(ctx.Port, "w3", accessor, "spp-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "spp-q", "tenant-x");
        await Task.Delay(1000);

        List<PartitionEntry> partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Single(partitions);
        Assert.True(partitions[0].Queue.Clients.Count() <= 2, $"Expected ≤2 consumers, got {partitions[0].Queue.Clients.Count()}");

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 6. Combination Scenario
    // ═══════════════════════════════════════════════════════════════════════

    #region Combination

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Combination_MaxPerWorker2_SubsPerPartition1_ThreeWorkers_SevenLabels(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "combo-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 2, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "combo-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "combo-q");
        HorseClient w3 = await BuildPoolWorker(ctx.Port, "w3", accessor, "combo-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 1; i <= 7; i++)
            await PushLabeled(producer, "combo-q", $"t-{i}");

        await Task.Delay(2000);

        Assert.Equal(7, queue.PartitionManager.Partitions.Count());
        int withConsumer = queue.PartitionManager.Partitions.Count(p => p.Queue.HasAnyClient());
        Assert.Equal(6, withConsumer); // 3 workers × 2 max each = 6
        Assert.Equal(6, tracker.TotalCount());

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 7. Label Case Insensitivity
    // ═══════════════════════════════════════════════════════════════════════

    #region Label Case

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task LabelCaseInsensitive_SamePartitionUsed(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "case-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "case-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "case-q", "TenantA");
        await PushLabeled(producer, "case-q", "tenanta");
        await PushLabeled(producer, "case-q", "TENANTA");

        await WaitUntil(() => tracker.TotalCount() >= 3, 5_000);
        Assert.Single(queue.PartitionManager.Partitions);
        Assert.Equal(3, tracker.TotalCount());

        producer.Disconnect();
        w.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 8. Round-Robin Without Label
    // ═══════════════════════════════════════════════════════════════════════

    #region Round Robin No Label

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_NoLabel_DistributesAcrossPartitions(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "rr-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "rr-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "rr-q", "p1");
        await PushLabeled(producer, "rr-q", "p2");
        await PushLabeled(producer, "rr-q", "p3");
        await WaitUntil(() => tracker.TotalCount() >= 3, 5_000);

        Assert.Equal(3, queue.PartitionManager.Partitions.Count());

        // Push 9 messages WITHOUT label → round-robin
        IHorseQueueBus rrBus = new HorseQueueBus(producer);
        for (int i = 0; i < 9; i++)
            await rrBus.Push("QueueA", new QueueAModel { TenantId = "rr", Sequence = i });

        await WaitUntil(() => tracker.TotalCount() >= 12);
        Assert.True(tracker.TotalCount() >= 12);

        producer.Disconnect();
        w.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 9. Disconnected Worker Cleanup
    // ═══════════════════════════════════════════════════════════════════════

    #region Disconnect Cleanup

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task DisconnectedWorker_CleanedFromPool_NotAssigned(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "dc-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "dc-q");
        await Task.Delay(300);
        w.Disconnect();
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "dc-q", "tenant-orphan");
        await Task.Delay(1000);

        Assert.Single(queue.PartitionManager.Partitions);
        Assert.False(queue.PartitionManager.Partitions.First().Queue.HasAnyClient());
        Assert.Equal(0, tracker.TotalCount());

        producer.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 10. FIFO Strict Ordering with WaitForAck
    // ═══════════════════════════════════════════════════════════════════════

    #region FIFO Ordering

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FIFO_SamePartition_WaitForAck_StrictOrdering(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "fifo-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        }, ack: QueueAckDecision.WaitForAcknowledge);
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "fifo-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        int msgCount = 30;
        for (int i = 0; i < msgCount; i++)
            await PushModel(producer, "t1", "t1", i);

        await WaitUntil(() => tracker.TotalCount() >= msgCount, 15_000);

        List<int> records = tracker.Get("QueueA", "t1");
        Assert.Equal(msgCount, records.Count);
        for (int i = 0; i < records.Count; i++)
            Assert.Equal(i, records[i]);

        producer.Disconnect();
        w.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 11. Partition Metrics Under Load
    // ═══════════════════════════════════════════════════════════════════════

    #region Metrics Under Load

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PartitionMetrics_UnderLoad_AccurateConsumerCounts(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "metrics-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 2,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 10, AutoDestroy = PartitionAutoDestroy.Disabled
        }, ack: QueueAckDecision.WaitForAcknowledge);
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "metrics-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "metrics-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int label = 1; label <= 5; label++)
        for (int i = 0; i < 10; i++)
            await PushModel(producer, $"L{label}", $"L{label}", i);

        await Task.Delay(2000);
        List<PartitionMetricSnapshot> metrics = queue.PartitionManager.GetMetrics().ToList();
        Assert.Equal(5, metrics.Count);
        Assert.All(metrics, m => Assert.True(m.ConsumerCount >= 1, $"Partition {m.Label} has 0 consumers"));

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 12. AutoDestroy = NoConsumers
    // ═══════════════════════════════════════════════════════════════════════

    #region AutoDestroy NoConsumers

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoDestroyNoConsumers_WorkerDisconnects_PartitionDestroyed(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "adnc-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5,
            AutoDestroy = PartitionAutoDestroy.NoConsumers, AutoDestroyIdleSeconds = 2
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "adnc-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "adnc-q", "tenant-x");
        await Task.Delay(500);
        Assert.Single(queue.PartitionManager.Partitions);

        w.Disconnect();
        await WaitUntil(() => !queue.PartitionManager.Partitions.Any());
        Assert.Empty(queue.PartitionManager.Partitions);

        producer.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 13. AutoDestroy = Empty
    // ═══════════════════════════════════════════════════════════════════════

    #region AutoDestroy Empty

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoDestroyEmpty_DestroyedOnlyWhenBothConditionsMet(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "ade-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5,
            AutoDestroy = PartitionAutoDestroy.Empty, AutoDestroyIdleSeconds = 2
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "ade-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "ade-q", "tenant-z");
        await WaitUntil(() => tracker.TotalCount() >= 1, 5_000);

        // Consumer still connected + no messages → AutoDestroy=Empty requires BOTH conditions
        await Task.Delay(4000);
        Assert.Single(queue.PartitionManager.Partitions);

        // Disconnect → no consumers AND no messages → destroyed
        w.Disconnect();
        await WaitUntil(() => !queue.PartitionManager.Partitions.Any());
        Assert.Empty(queue.PartitionManager.Partitions);

        producer.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 14. Multiple Workers Same Partition
    // ═══════════════════════════════════════════════════════════════════════

    #region Multiple Workers Same Partition

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MultipleWorkersSamePartition_BothAssigned(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "mwsp-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 3,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "mwsp-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "mwsp-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 10; i++)
            await PushLabeled(producer, "mwsp-q", "shared-label");

        await WaitUntil(() => tracker.TotalCount() >= 10);

        Assert.Single(queue.PartitionManager.Partitions);
        Assert.True(queue.PartitionManager.Partitions.First().Queue.Clients.Count() >= 2);
        Assert.Equal(10, tracker.TotalCount());

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 15. Partition Creation Idempotency
    // ═══════════════════════════════════════════════════════════════════════

    #region Idempotency

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SameLabelPushedMultipleTimes_OnlyOnePartitionCreated(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "idem-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "idem-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 20; i++)
            await PushLabeled(producer, "idem-q", "same-label");

        await WaitUntil(() => tracker.TotalCount() >= 20);
        Assert.Single(queue.PartitionManager.Partitions);
        Assert.Equal(20, tracker.TotalCount());

        producer.Disconnect();
        w.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 16. Late Worker Join
    // ═══════════════════════════════════════════════════════════════════════

    #region Late Worker Join

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task LateWorkerJoin_MessagesAlreadyQueued_WorkerConsumesBacklog(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "late-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 5; i++)
            await PushLabeled(producer, "late-q", "early-bird");

        await Task.Delay(500);
        Assert.Single(queue.PartitionManager.Partitions);
        Assert.False(queue.PartitionManager.Partitions.First().Queue.HasAnyClient());

        // Connect worker late — should consume backlog
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "late-q");

        await WaitUntil(() => tracker.TotalCount() >= 5);
        Assert.Equal(5, tracker.TotalCount());

        producer.Disconnect();
        w.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 17. Many Partitions + Few Workers Stress
    // ═══════════════════════════════════════════════════════════════════════

    #region Stress

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ManyPartitions_FewWorkers_AllMessagesConsumed(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "stress-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 25, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "stress-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "stress-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        int labelCount = 50;
        for (int i = 0; i < labelCount; i++)
            await PushLabeled(producer, "stress-q", $"t-{i}");

        await WaitUntil(() => tracker.TotalCount() >= labelCount, 15_000);
        Assert.Equal(labelCount, queue.PartitionManager.Partitions.Count());
        Assert.Equal(labelCount, tracker.TotalCount());
        Assert.All(queue.PartitionManager.Partitions, p => Assert.True(p.Queue.HasAnyClient()));

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    #endregion

    #region Server Setup Helpers

    private static async Task<PartitionTestContext> CreateServer(string mode)
    {
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.JustRequest;
            opts.AutoQueueCreation = true;
        });

        foreach (string qName in new[] { "QueueA", "QueueB", "QueueC" })
            await ctx.Rider.Queue.Create(qName, o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.JustRequest;
                o.Partition = new PartitionOptions
                {
                    Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 10,
                    AutoAssignWorkers = true, MaxPartitionsPerWorker = 3,
                    AutoDestroy = PartitionAutoDestroy.Disabled
                };
            });

        ctx.Rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 10,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 3,
            AutoDestroy = PartitionAutoDestroy.Disabled
        };

        return ctx;
    }

    private static async Task<(PartitionTestContext ctx, HorseQueue queue)> CreateSingleQueueServer(
        string mode, PartitionOptions partitionOptions,
        QueueAckDecision ack = QueueAckDecision.JustRequest)
    {
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = ack;
            opts.AutoQueueCreation = true;
        });

        await ctx.Rider.Queue.Create("QueueA", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.Acknowledge = ack;
            o.Partition = partitionOptions;
        });

        return (ctx, ctx.Rider.Queue.Find("QueueA"));
    }

    /// <summary>Backward-compatible overload — ignores queueName, always creates "QueueA".</summary>
    private static Task<(PartitionTestContext ctx, HorseQueue queue)> CreateSingleQueueServer(
        string mode, string queueName, PartitionOptions partitionOptions,
        QueueType queueType = QueueType.RoundRobin,
        QueueAckDecision ack = QueueAckDecision.JustRequest)
        => CreateSingleQueueServer(mode, partitionOptions, ack);

    #endregion

    #region Worker / Producer Builders

    private static HorseClient BuildFullWorker(int port, string clientType, TrackerAccessor accessor,
        string label, int maxPartitions = 3, int subscribersPerPartition = 10)
    {
        ServiceCollection services = new();
        services.AddSingleton(accessor);
        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{port}");
        builder.SetClientType(clientType);
        builder.AutoSubscribe(true);

        builder.AddScopedConsumer<QueueAConsumer>(label, maxPartitions, subscribersPerPartition);
        builder.AddScopedConsumer<QueueBConsumer>(label, maxPartitions, subscribersPerPartition);
        builder.AddScopedConsumer<QueueCConsumer>(label, maxPartitions, subscribersPerPartition);
        builder.AddScopedConsumer<QueueDConsumer>(name => $"{name}-{label}", true);

        return builder.Build();
    }

    /// <summary>
    ///     Single-queue pool worker: subscribes to "QueueA" via enterWorkerPool.
    ///     Uses DI-based QueueAConsumer — messages are QueueAModel.
    /// </summary>
    private static HorseClient BuildPoolWorker(int port, string clientType, TrackerAccessor accessor)
    {
        ServiceCollection services = new();
        services.AddSingleton(accessor);
        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{port}");
        builder.SetClientType(clientType);
        builder.AutoSubscribe(true);
        builder.AddScopedConsumer<QueueAConsumer>(name => name, enterWorkerPool: true);
        return builder.Build();
    }

    /// <summary>Backward-compatible overload — ignores queueName, builds, connects and returns.</summary>
    private static async Task<HorseClient> BuildPoolWorker(int port, string clientType, TrackerAccessor accessor, string queueName)
    {
        HorseClient client = BuildPoolWorker(port, clientType, accessor);
        await client.ConnectAsync();
        return client;
    }

    private static async Task<HorseClient> CreateProducer(int port)
    {
        HorseClient client = new();
        client.SetClientType("producer");
        await client.ConnectAsync($"horse://localhost:{port}");
        Assert.True(client.IsConnected);
        return client;
    }

    /// <summary>Push a QueueAModel with partition label to "QueueA" via IHorseQueueBus.</summary>
    private static async Task PushModel(HorseClient producer, string label, string tenantId = null, int sequence = 0)
    {
        tenantId ??= label;
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("QueueA", new QueueAModel { TenantId = tenantId, Sequence = sequence }, partitionLabel: label);
    }

    /// <summary>Backward-compatible overload — ignores queue name, delegates to PushModel.</summary>
    private static Task PushLabeled(HorseClient producer, string queue, string label, string body = null)
        => PushModel(producer, label);

    #endregion

    #region Shared Helpers

    private static async Task WaitForConsume(ConsumeTracker tracker, int expected, int timeoutMs)
    {
        int elapsed = 0;
        while (elapsed < timeoutMs)
        {
            if (tracker.TotalCount() >= expected) return;
            await Task.Delay(100);
            elapsed += 100;
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        int elapsed = 0;
        while (!condition() && elapsed < timeoutMs)
        {
            await Task.Delay(100);
            elapsed += 100;
        }
    }

    private static List<List<int>> SplitIntoOrderedChunks(List<int> records)
    {
        List<List<int>> chunks = new();
        List<int> current = new();
        foreach (int seq in records)
        {
            if (seq == 0 && current.Count > 0)
            {
                chunks.Add(current);
                current = new List<int>();
            }
            current.Add(seq);
        }
        if (current.Count > 0) chunks.Add(current);
        return chunks;
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 1. Original Multi-Queue / Multi-Tier Tests
    // ═══════════════════════════════════════════════════════════════════════

    #region Multi-Queue Multi-Tier Tests

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task QueueABC_ThreeTiers_MessagesConsumedByCorrectLabel(string mode)
    {
        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        await using PartitionTestContext ctx = await CreateServer(mode);

        HorseClient w1 = BuildFullWorker(ctx.Port, "w-free", accessor, "free");
        HorseClient w2 = BuildFullWorker(ctx.Port, "w-standard", accessor, "standard");
        HorseClient w3 = BuildFullWorker(ctx.Port, "w-premium", accessor, "premium");
        await w1.ConnectAsync();
        await w2.ConnectAsync();
        await w3.ConnectAsync();
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);

        string[] labels = { "free", "standard", "premium" };
        string[] queues = { "QueueA", "QueueB", "QueueC" };

        foreach (string qName in queues)
        foreach (string label in labels)
            for (int i = 0; i < 5; i++)
                switch (qName)
                {
                    case "QueueA": await bus.Push(qName, new QueueAModel { TenantId = label, Sequence = i }, partitionLabel: label); break;
                    case "QueueB": await bus.Push(qName, new QueueBModel { TenantId = label, Sequence = i }, partitionLabel: label); break;
                    case "QueueC": await bus.Push(qName, new QueueCModel { TenantId = label, Sequence = i }, partitionLabel: label); break;
                }

        await WaitForConsume(tracker, 45, 15_000);

        foreach (string qName in queues)
        foreach (string label in labels)
            Assert.Equal(5, tracker.Get(qName, label).Count);

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task QueueD_WithNameTransform_100Tenants_FIFOOrdering(string mode)
    {
        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        await using PartitionTestContext ctx = await CreateServer(mode);

        foreach (string tier in new[] { "free", "standard", "premium" })
            await ctx.Rider.Queue.Create($"QueueD-{tier}", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.Partition = new PartitionOptions
                {
                    Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 10, AutoAssignWorkers = true, MaxPartitionsPerWorker = 200,
                    AutoDestroy = PartitionAutoDestroy.Disabled
                };
            });

        HorseClient w1 = BuildFullWorker(ctx.Port, "w-free", accessor, "free");
        HorseClient w2 = BuildFullWorker(ctx.Port, "w-standard", accessor, "standard");
        HorseClient w3 = BuildFullWorker(ctx.Port, "w-premium", accessor, "premium");
        await w1.ConnectAsync();
        await w2.ConnectAsync();
        await w3.ConnectAsync();
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);

        string[] tiers = { "free", "standard", "premium" };
        int tenantCount = 10, msgsPerTenant = 5;
        string[] tenantIds = Enumerable.Range(0, tenantCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
        int expectedTotal = tenantCount * msgsPerTenant * tiers.Length;

        foreach (string tier in tiers)
        {
            string targetQueue = $"QueueD-{tier}";
            for (int t = 0; t < tenantCount; t++)
            for (int seq = 0; seq < msgsPerTenant; seq++)
                await bus.Push(targetQueue, new QueueDModel { TenantId = tenantIds[t], Sequence = seq }, partitionLabel: tenantIds[t]);
        }

        await WaitForConsume(tracker, expectedTotal, 120_000);

        foreach (string tenantId in tenantIds)
        {
            List<int> records = tracker.Get("QueueD", tenantId);
            Assert.Equal(msgsPerTenant * tiers.Length, records.Count);
            List<List<int>> chunks = SplitIntoOrderedChunks(records);
            Assert.Equal(tiers.Length, chunks.Count);
            foreach (List<int> chunk in chunks)
            {
                Assert.Equal(msgsPerTenant, chunk.Count);
                for (int i = 0; i < chunk.Count; i++) Assert.Equal(i, chunk[i]);
            }
        }

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AllQueues_CombinedScenario_FullEnd2End(string mode)
    {
        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        await using PartitionTestContext ctx = await CreateServer(mode);

        foreach (string tier in new[] { "free", "standard", "premium" })
            await ctx.Rider.Queue.Create($"QueueD-{tier}", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.Partition = new PartitionOptions
                {
                    Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 10, AutoAssignWorkers = true, MaxPartitionsPerWorker = 50,
                    AutoDestroy = PartitionAutoDestroy.Disabled
                };
            });

        HorseClient w1 = BuildFullWorker(ctx.Port, "w-free", accessor, "free");
        HorseClient w2 = BuildFullWorker(ctx.Port, "w-standard", accessor, "standard");
        HorseClient w3 = BuildFullWorker(ctx.Port, "w-premium", accessor, "premium");
        await w1.ConnectAsync();
        await w2.ConnectAsync();
        await w3.ConnectAsync();
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);

        string[] labels = { "free", "standard", "premium" };
        int tenantCount = 10, msgsPerTenant = 5;
        string[] tenantIds = Enumerable.Range(0, tenantCount).Select(_ => Guid.NewGuid().ToString("N")).ToArray();

        int abcExpected = 0;
        foreach (string qName in new[] { "QueueA", "QueueB", "QueueC" })
        foreach (string label in labels)
            for (int seq = 0; seq < 3; seq++)
            {
                switch (qName)
                {
                    case "QueueA": await bus.Push(qName, new QueueAModel { TenantId = label, Sequence = seq }, partitionLabel: label); break;
                    case "QueueB": await bus.Push(qName, new QueueBModel { TenantId = label, Sequence = seq }, partitionLabel: label); break;
                    case "QueueC": await bus.Push(qName, new QueueCModel { TenantId = label, Sequence = seq }, partitionLabel: label); break;
                }
                abcExpected++;
            }

        int dExpected = 0;
        foreach (string tier in labels)
        {
            string targetQueue = $"QueueD-{tier}";
            for (int t = 0; t < tenantCount; t++)
            for (int seq = 0; seq < msgsPerTenant; seq++)
            {
                await bus.Push(targetQueue, new QueueDModel { TenantId = tenantIds[t], Sequence = seq }, partitionLabel: tenantIds[t]);
                dExpected++;
            }
        }

        await WaitForConsume(tracker, abcExpected + dExpected, 30_000);

        foreach (string qName in new[] { "QueueA", "QueueB", "QueueC" })
        foreach (string label in labels)
            Assert.Equal(3, tracker.Get(qName, label).Count);

        foreach (string tenantId in tenantIds)
        {
            List<int> records = tracker.Get("QueueD", tenantId);
            Assert.Equal(msgsPerTenant * labels.Length, records.Count);
            List<List<int>> chunks = SplitIntoOrderedChunks(records);
            Assert.Equal(labels.Length, chunks.Count);
            foreach (List<int> chunk in chunks)
            {
                Assert.Equal(msgsPerTenant, chunk.Count);
                for (int i = 0; i < chunk.Count; i++) Assert.Equal(i, chunk[i]);
            }
        }

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Workers_SubscribeToPartitions_PartitionManagerShowsCorrectCounts(string mode)
    {
        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        await using PartitionTestContext ctx = await CreateServer(mode);

        HorseClient w1 = BuildFullWorker(ctx.Port, "w-free", accessor, "free");
        HorseClient w2 = BuildFullWorker(ctx.Port, "w-standard", accessor, "standard");
        HorseClient w3 = BuildFullWorker(ctx.Port, "w-premium", accessor, "premium");
        await w1.ConnectAsync();
        await w2.ConnectAsync();
        await w3.ConnectAsync();
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);

        foreach (string label in new[] { "free", "standard", "premium" })
            await bus.Push("QueueA", new QueueAModel { TenantId = label, Sequence = 0 }, partitionLabel: label);

        await Task.Delay(1000);

        HorseQueue qa = ctx.Rider.Queue.Find("QueueA");
        Assert.NotNull(qa?.PartitionManager);
        List<PartitionMetricSnapshot> metrics = qa.PartitionManager.GetMetrics().ToList();
        Assert.Equal(3, metrics.Count);
        Assert.All(metrics, m => Assert.True(m.ConsumerCount >= 1));

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task QueueD_NameTransform_QueuesAutoCreated_WorkersAssigned(string mode)
    {
        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        await using PartitionTestContext ctx = await CreateServer(mode);

        foreach (string tier in new[] { "free", "standard", "premium" })
            await ctx.Rider.Queue.Create($"QueueD-{tier}", o =>
            {
                o.Type = QueueType.RoundRobin;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                o.Partition = new PartitionOptions
                {
                    Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 10, AutoAssignWorkers = true, MaxPartitionsPerWorker = 50,
                    AutoDestroy = PartitionAutoDestroy.Disabled
                };
            });

        HorseClient w1 = BuildFullWorker(ctx.Port, "w-free", accessor, "free");
        HorseClient w2 = BuildFullWorker(ctx.Port, "w-standard", accessor, "standard");
        HorseClient w3 = BuildFullWorker(ctx.Port, "w-premium", accessor, "premium");
        await w1.ConnectAsync();
        await w2.ConnectAsync();
        await w3.ConnectAsync();
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);

        string tenantId = Guid.NewGuid().ToString("N");
        foreach (string tier in new[] { "free", "standard", "premium" })
            await bus.Push($"QueueD-{tier}", new QueueDModel { TenantId = tenantId, Sequence = 1 }, partitionLabel: tenantId);

        await WaitForConsume(tracker, 3, 10_000);
        Assert.Equal(3, tracker.Get("QueueD", tenantId).Count);

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 3. AutoDestroy = NoMessages
    // ═══════════════════════════════════════════════════════════════════════

    #region AutoDestroy NoMessages

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoDestroyNoMessages_PartitionDestroyedAfterAllConsumed(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "ad-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5,
            AutoDestroy = PartitionAutoDestroy.NoMessages, AutoDestroyIdleSeconds = 2
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "ad-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "ad-q", "tenant-1");
        await PushLabeled(producer, "ad-q", "tenant-1");

        await WaitUntil(() => tracker.TotalCount() >= 2, 5_000);
        await WaitUntil(() => !queue.PartitionManager.Partitions.Any());
        Assert.Empty(queue.PartitionManager.Partitions);

        producer.Disconnect();
        w.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoDestroyNoMessages_OnlyEmptyPartitionsDestroyed(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "ad-multi-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5,
            AutoDestroy = PartitionAutoDestroy.NoMessages, AutoDestroyIdleSeconds = 3
        }, ack: QueueAckDecision.WaitForAcknowledge);
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "ad-multi-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        // tenant-1 gets just 1 message (will be consumed fast → partition becomes empty)
        await PushLabeled(producer, "ad-multi-q", "tenant-1");

        // tenant-2: push messages continuously so it never drains
        bool keepPushing = true;
        Task pushTask = Task.Run(async () =>
        {
            while (keepPushing)
            {
                await PushLabeled(producer, "ad-multi-q", "tenant-2");
                await Task.Delay(10);
            }
        });

        // Wait until tenant-1 is consumed
        await WaitUntil(() => tracker.TotalCount() >= 1, 10_000);

        // Wait for auto-destroy to remove tenant-1 while tenant-2 is still active
        await WaitUntil(() =>
        {
            List<PartitionEntry> parts = queue.PartitionManager.Partitions.ToList();
            return parts.Count == 1 && parts[0].Label == "tenant-2";
        }, 30_000);

        keepPushing = false;
        await pushTask;

        List<PartitionEntry> remaining = queue.PartitionManager.Partitions.ToList();
        Assert.Single(remaining);
        Assert.Equal("tenant-2", remaining[0].Label);

        producer.Disconnect();
        w.Disconnect();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 4. MaxPartitionsPerWorker Scenarios
    // ═══════════════════════════════════════════════════════════════════════

    #region MaxPartitionsPerWorker

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MaxPartitionsPerWorker1_WorkerOnlyServesOnePartition(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "mpw1-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 1, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "mpw1-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "mpw1-q", "label-a");
        await PushLabeled(producer, "mpw1-q", "label-b");
        await Task.Delay(1000);

        List<PartitionEntry> partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(2, partitions.Count);
        Assert.Equal(1, partitions.Count(p => p.Queue.HasAnyClient()));
        Assert.Equal(1, tracker.TotalCount());

        producer.Disconnect();
        w.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MaxPartitionsPerWorker2_WorkerServesTwoPartitions(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "mpw2-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 2, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "mpw2-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "mpw2-q", "label-a");
        await PushLabeled(producer, "mpw2-q", "label-b");
        await PushLabeled(producer, "mpw2-q", "label-c");
        await Task.Delay(1000);

        List<PartitionEntry> partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(3, partitions.Count);
        Assert.Equal(2, partitions.Count(p => p.Queue.HasAnyClient()));
        Assert.Equal(2, tracker.TotalCount());

        producer.Disconnect();
        w.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MaxPartitionsPerWorker0_Unlimited_WorkerServesAll(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "mpw0-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 0, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "mpw0-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 5; i++)
            await PushLabeled(producer, "mpw0-q", $"label-{i}");

        await WaitUntil(() => tracker.TotalCount() >= 5, 5_000);
        Assert.Equal(5, queue.PartitionManager.Partitions.Count());
        Assert.All(queue.PartitionManager.Partitions, p => Assert.True(p.Queue.HasAnyClient()));

        producer.Disconnect();
        w.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task WorkerAtCapacity_SkippedDuringAssignment(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "cap-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 1, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "cap-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "cap-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "cap-q", "a");
        await PushLabeled(producer, "cap-q", "b");
        await PushLabeled(producer, "cap-q", "c");
        await Task.Delay(1000);

        Assert.Equal(3, queue.PartitionManager.Partitions.Count());
        Assert.Equal(2, queue.PartitionManager.Partitions.Count(p => p.Queue.HasAnyClient()));
        Assert.Equal(2, tracker.TotalCount());

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    #endregion
}