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
using Horse.Messaging.Server.Clients;
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

/// <summary>
/// Model whose attribute overrides server default MaxPartitionCount to 1.
/// Queue auto-created with MaxPartitionCount=1 → only 1 partition allowed.
/// </summary>
[QueueName("LimitedQ")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.JustRequest)]
[PartitionedQueue(MaxPartitions = 1, SubscribersPerPartition = 1)]
public class LimitedPartitionModel
{
    public string Data { get; set; }
}

/// <summary>
/// Model whose attribute overrides server default MaxPartitionCount to 0 (unlimited).
/// Queue auto-created with MaxPartitionCount=0 → unlimited partitions.
/// </summary>
[QueueName("UnlimitedQ")]
[QueueType(MessagingQueueType.RoundRobin)]
[Acknowledge(QueueAckDecision.JustRequest)]
[PartitionedQueue(MaxPartitions = 0, SubscribersPerPartition = 1)]
public class UnlimitedPartitionModel
{
    public string Data { get; set; }
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

[AutoAck]
public class LimitedPartitionConsumer(TrackerAccessor accessor) : IQueueConsumer<LimitedPartitionModel>
{
    public Task Consume(HorseMessage message, LimitedPartitionModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        accessor.Tracker.Record("LimitedQ", model.Data, 0);
        return Task.CompletedTask;
    }
}

[AutoAck]
public class UnlimitedPartitionConsumer(TrackerAccessor accessor) : IQueueConsumer<UnlimitedPartitionModel>
{
    public Task Consume(HorseMessage message, UnlimitedPartitionModel model, HorseClient client, CancellationToken cancellationToken = default)
    {
        accessor.Tracker.Record("UnlimitedQ", model.Data, 0);
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
                    case "QueueA": await bus.Push(qName, new QueueAModel { TenantId = label, Sequence = i }, false, null, label, CancellationToken.None); break;
                    case "QueueB": await bus.Push(qName, new QueueBModel { TenantId = label, Sequence = i }, false, null, label, CancellationToken.None); break;
                    case "QueueC": await bus.Push(qName, new QueueCModel { TenantId = label, Sequence = i }, false, null, label, CancellationToken.None); break;
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
                await bus.Push(targetQueue, new QueueDModel { TenantId = tenantIds[t], Sequence = seq }, false, null, tenantIds[t], CancellationToken.None);
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
                    case "QueueA": await bus.Push(qName, new QueueAModel { TenantId = label, Sequence = seq }, false, null, label, CancellationToken.None); break;
                    case "QueueB": await bus.Push(qName, new QueueBModel { TenantId = label, Sequence = seq }, false, null, label, CancellationToken.None); break;
                    case "QueueC": await bus.Push(qName, new QueueCModel { TenantId = label, Sequence = seq }, false, null, label, CancellationToken.None); break;
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
                await bus.Push(targetQueue, new QueueDModel { TenantId = tenantIds[t], Sequence = seq }, false, null, tenantIds[t], CancellationToken.None);
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
            await bus.Push("QueueA", new QueueAModel { TenantId = label, Sequence = 0 }, false, null, label, CancellationToken.None);

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
            await bus.Push($"QueueD-{tier}", new QueueDModel { TenantId = tenantId, Sequence = 1 }, false, null, tenantId, CancellationToken.None);

        await WaitForConsume(tracker, 3, 10_000);
        Assert.Equal(3, tracker.Get("QueueD", tenantId).Count);

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    #endregion
    
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
            AutoDestroy = PartitionAutoDestroy.NoMessages, AutoDestroyIdleSeconds = 2
        }, ack: QueueAckDecision.WaitForAcknowledge);
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w = await BuildPoolWorker(ctx.Port, "w1", accessor, "ad-multi-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);

        // tenant-2: start pushing continuously BEFORE tenant-1 so it's always non-empty
        bool keepPushing = true;
        Task pushTask = Task.Run(async () =>
        {
            while (keepPushing)
            {
                try { await PushLabeled(producer, "ad-multi-q", "tenant-2"); }
                catch { /* producer may disconnect at end */ }
                await Task.Delay(5);
            }
        });

        // Small delay to let tenant-2 partition be created and start receiving
        await Task.Delay(500);

        // tenant-1 gets just 1 message (will be consumed fast → partition becomes empty)
        await PushLabeled(producer, "ad-multi-q", "tenant-1");

        // Wait until tenant-1 is consumed
        await WaitUntil(() => tracker.TotalCount() >= 2, 10_000);

        // Wait for auto-destroy to remove tenant-1.
        // Condition: tenant-1 partition no longer exists. tenant-2 should survive.
        await WaitUntil(() =>
        {
            List<PartitionEntry> parts = queue.PartitionManager.Partitions.ToList();
            // tenant-1 must be gone
            return parts.All(p => p.Label != "tenant-1") && parts.Any(p => p.Label == "tenant-2");
        }, 30_000);

        keepPushing = false;
        await pushTask;

        List<PartitionEntry> remaining = queue.PartitionManager.Partitions.ToList();
        Assert.DoesNotContain(remaining, p => p.Label == "tenant-1");
        Assert.Contains(remaining, p => p.Label == "tenant-2");

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

    /// <summary>
    /// Two separate parent queues each with their own worker pool.
    /// Worker 1 subscribes (pool) to IsolatedQ-A only.
    /// Worker 2 subscribes (pool) to IsolatedQ-B only.
    /// Messages pushed to IsolatedQ-A must ONLY be consumed by Worker 1.
    /// Messages pushed to IsolatedQ-B must ONLY be consumed by Worker 2.
    /// Worker pools are per-PartitionManager (per-queue) — no cross-queue assignment should occur.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task WorkerPool_Isolation_AcrossParentQueues_NoCrossAssignment(string mode)
    {
        // Create server with 2 separate partitioned queues
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.JustRequest;
            opts.AutoQueueCreation = false;
        });
        await using var _ = ctx;

        PartitionOptions partOpts = new()
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 5, AutoDestroy = PartitionAutoDestroy.Disabled
        };

        await ctx.Rider.Queue.Create("IsolatedQ-A", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.Partition = partOpts;
        });

        await ctx.Rider.Queue.Create("IsolatedQ-B", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.Partition = partOpts;
        });

        HorseQueue queueA = ctx.Rider.Queue.Find("IsolatedQ-A");
        HorseQueue queueB = ctx.Rider.Queue.Find("IsolatedQ-B");
        Assert.NotNull(queueA);
        Assert.NotNull(queueB);

        // Track which clientType received messages from which queue
        ConcurrentBag<string> receivedByW1 = new();
        ConcurrentBag<string> receivedByW2 = new();

        // Worker 1: subscribes to IsolatedQ-A pool only
        HorseClient w1 = new();
        w1.SetClientType("w1");
        w1.AutoAcknowledge = true;
        w1.MessageReceived += (_, msg) =>
        {
            receivedByW1.Add(msg.Target); // partition queue name contains parent info
        };
        await w1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await w1.Queue.Subscribe("IsolatedQ-A", true, CancellationToken.None);
        await Task.Delay(200);

        // Worker 2: subscribes to IsolatedQ-B pool only
        HorseClient w2 = new();
        w2.SetClientType("w2");
        w2.AutoAcknowledge = true;
        w2.MessageReceived += (_, msg) =>
        {
            receivedByW2.Add(msg.Target);
        };
        await w2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await w2.Queue.Subscribe("IsolatedQ-B", true, CancellationToken.None);
        await Task.Delay(200);

        // Push labeled messages to each queue
        HorseClient producer = await CreateProducer(ctx.Port);

        for (int i = 0; i < 5; i++)
        {
            await producer.Queue.Push("IsolatedQ-A",
                System.Text.Encoding.UTF8.GetBytes($"a-msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"tenant-a-{i}") }, CancellationToken.None);

            await producer.Queue.Push("IsolatedQ-B",
                System.Text.Encoding.UTF8.GetBytes($"b-msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"tenant-b-{i}") }, CancellationToken.None);
        }

        await WaitUntil(() => receivedByW1.Count >= 5 && receivedByW2.Count >= 5, 10_000);

        // W1 should only have received messages from IsolatedQ-A partitions
        Assert.True(receivedByW1.Count >= 5, $"W1 expected ≥5, got {receivedByW1.Count}");
        Assert.All(receivedByW1, target => Assert.Contains("IsolatedQ-A", target));

        // W2 should only have received messages from IsolatedQ-B partitions
        Assert.True(receivedByW2.Count >= 5, $"W2 expected ≥5, got {receivedByW2.Count}");
        Assert.All(receivedByW2, target => Assert.Contains("IsolatedQ-B", target));

        // Cross check: no IsolatedQ-B in W1, no IsolatedQ-A in W2
        Assert.DoesNotContain(receivedByW1, t => t.Contains("IsolatedQ-B"));
        Assert.DoesNotContain(receivedByW2, t => t.Contains("IsolatedQ-A"));

        // Verify partition counts
        Assert.Equal(5, queueA.PartitionManager.Partitions.Count());
        Assert.Equal(5, queueB.PartitionManager.Partitions.Count());

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
            await rrBus.Push("rr-q", new QueueAModel { TenantId = "rr", Sequence = i }, false, CancellationToken.None);

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
            await PushModel(producer, "fifo-q", "t1", "t1", i);

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
            await PushModel(producer, "metrics-q", $"L{label}", $"L{label}", i);

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
        string mode, string queueName, PartitionOptions partitionOptions,
        QueueType queueType = QueueType.RoundRobin,
        QueueAckDecision ack = QueueAckDecision.JustRequest)
    {
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = queueType;
            opts.Acknowledge = ack;
            opts.AutoQueueCreation = true;
        });

        await ctx.Rider.Queue.Create(queueName, o =>
        {
            o.Type = queueType;
            o.Acknowledge = ack;
            o.Partition = partitionOptions;
        });

        return (ctx, ctx.Rider.Queue.Find(queueName));
    }

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
    ///     Single-queue pool worker: subscribes to the specified queue via enterWorkerPool.
    ///     Uses DI-based QueueAConsumer — messages are QueueAModel.
    ///     Queue name is overridden from the attribute's "QueueA" to the given queueName.
    /// </summary>
    private static async Task<HorseClient> BuildPoolWorker(int port, string clientType, TrackerAccessor accessor, string queueName)
    {
        ServiceCollection services = new();
        services.AddSingleton(accessor);
        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{port}");
        builder.SetClientType(clientType);
        builder.AutoSubscribe(true);
        builder.AddScopedConsumer<QueueAConsumer>(name => queueName, enterWorkerPool: true);
        HorseClient client = builder.Build();
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

    /// <summary>Push a QueueAModel with partition label to the specified queue via IHorseQueueBus.</summary>
    private static async Task PushModel(HorseClient producer, string queue, string label, string tenantId = null, int sequence = 0)
    {
        tenantId ??= label;
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push(queue, new QueueAModel { TenantId = tenantId, Sequence = sequence }, false, null, label, CancellationToken.None);
    }

    /// <summary>Push a QueueAModel with partition label to the specified queue.</summary>
    private static Task PushLabeled(HorseClient producer, string queue, string label, string body = null)
        => PushModel(producer, queue, label);

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
    // 18. Worker Fair Distribution (Least-Loaded Strategy)
    // ═══════════════════════════════════════════════════════════════════════

    #region Worker Fair Distribution

    /// <summary>
    /// 10 workers, MaxPartitionsPerWorker=0 (unlimited), 10 labeled partitions, SubscribersPerPartition=1.
    /// Each partition should get exactly 1 different worker — no single worker should hog all partitions.
    /// Verifies TryAssignPooledWorker's least-loaded-first selection.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FairDistribution_10Workers_10Labels_EachWorkerGetsOnePartition(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "fair10-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 0, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);

        List<HorseClient> workers = new();
        for (int i = 0; i < 10; i++)
        {
            HorseClient w = await BuildPoolWorker(ctx.Port, $"w{i}", accessor, "fair10-q");
            workers.Add(w);
        }
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 10; i++)
            await PushLabeled(producer, "fair10-q", $"tenant-{i}");

        await WaitUntil(() => tracker.TotalCount() >= 10, 10_000);

        Assert.Equal(10, queue.PartitionManager.Partitions.Count());
        Assert.All(queue.PartitionManager.Partitions, p => Assert.True(p.Queue.HasAnyClient()));

        HashSet<string> assignedClientIds = new();
        foreach (PartitionEntry p in queue.PartitionManager.Partitions)
        {
            var clients = p.Queue.Clients.ToList();
            Assert.Single(clients);
            assignedClientIds.Add(clients[0].Client.UniqueId);
        }

        // All 10 unique workers should be used
        Assert.Equal(10, assignedClientIds.Count);

        producer.Disconnect();
        foreach (HorseClient w in workers) w.Disconnect();
    }

    /// <summary>
    /// 3 workers, MaxPartitionsPerWorker=0, 9 labeled partitions, SubscribersPerPartition=1.
    /// Each worker should get ~3 partitions (9 / 3 = 3).
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FairDistribution_3Workers_9Labels_EachGets3Partitions(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "fair9-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 0, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "fair9-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "fair9-q");
        HorseClient w3 = await BuildPoolWorker(ctx.Port, "w3", accessor, "fair9-q");
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 9; i++)
            await PushLabeled(producer, "fair9-q", $"tenant-{i}");

        await WaitUntil(() => tracker.TotalCount() >= 9, 10_000);

        Assert.Equal(9, queue.PartitionManager.Partitions.Count());
        Assert.All(queue.PartitionManager.Partitions, p => Assert.True(p.Queue.HasAnyClient()));

        Dictionary<string, int> assignmentCounts = new();
        foreach (PartitionEntry p in queue.PartitionManager.Partitions)
        foreach (QueueClient qc in p.Queue.Clients)
        {
            string id = qc.Client.UniqueId;
            assignmentCounts[id] = assignmentCounts.GetValueOrDefault(id, 0) + 1;
        }

        Assert.Equal(3, assignmentCounts.Count);
        Assert.All(assignmentCounts.Values, count => Assert.InRange(count, 2, 4));

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
        w3.Disconnect();
    }

    /// <summary>
    /// SubscribersPerPartition=3, MaxPartitionsPerWorker=0, 5 workers, 2 partitions (6 total slots).
    /// First worker should NOT grab all 6 slots — at least 4 of 5 workers should be assigned.
    /// Verifies TryAssignToExistingPartition fair-share cap.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FairDistribution_SubsPerPartition3_NoGreedyFirstWorker(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "fair-spp-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 3,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 0, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);

        HorseClient producer = await CreateProducer(ctx.Port);
        await PushLabeled(producer, "fair-spp-q", "part-1");
        await PushLabeled(producer, "fair-spp-q", "part-2");
        await Task.Delay(300);

        Assert.Equal(2, queue.PartitionManager.Partitions.Count());

        List<HorseClient> workers = new();
        for (int i = 0; i < 5; i++)
        {
            HorseClient w = await BuildPoolWorker(ctx.Port, $"w{i}", accessor, "fair-spp-q");
            workers.Add(w);
            await Task.Delay(200);
        }
        await Task.Delay(500);

        HashSet<string> assignedWorkerIds = new();
        foreach (PartitionEntry p in queue.PartitionManager.Partitions)
        foreach (QueueClient qc in p.Queue.Clients)
            assignedWorkerIds.Add(qc.Client.UniqueId);

        Assert.True(assignedWorkerIds.Count >= 4,
            $"Expected ≥4 unique workers assigned, got {assignedWorkerIds.Count}. First worker should not hog all slots.");

        Assert.All(queue.PartitionManager.Partitions, p =>
            Assert.True(p.Queue.Clients.Count() <= 3, $"Partition {p.PartitionId} has {p.Queue.Clients.Count()} consumers, max is 3"));

        producer.Disconnect();
        foreach (HorseClient w in workers) w.Disconnect();
    }

    /// <summary>
    /// 2 workers, MaxPartitionsPerWorker=0, 20 labeled partitions, SubscribersPerPartition=1.
    /// Each worker should get ~10 partitions. Neither should get all 20.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FairDistribution_2Workers_20Labels_BalancedAssignment(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "fair20-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 0, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);
        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "fair20-q");
        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "fair20-q");
        await Task.Delay(500);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 20; i++)
            await PushLabeled(producer, "fair20-q", $"t-{i}");

        await WaitUntil(() => tracker.TotalCount() >= 20, 15_000);

        Assert.Equal(20, queue.PartitionManager.Partitions.Count());

        Dictionary<string, int> assignmentCounts = new();
        foreach (PartitionEntry p in queue.PartitionManager.Partitions)
        foreach (QueueClient qc in p.Queue.Clients)
        {
            string id = qc.Client.UniqueId;
            assignmentCounts[id] = assignmentCounts.GetValueOrDefault(id, 0) + 1;
        }

        Assert.Equal(2, assignmentCounts.Count);
        Assert.All(assignmentCounts.Values, count => Assert.InRange(count, 7, 13));

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    /// <summary>
    /// Workers arrive staggered. Worker 1 gets first 4 partitions.
    /// Worker 2 arrives late and should get assigned to new partitions via least-loaded strategy.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task FairDistribution_StaggeredWorkerArrival_LateWorkersGetAssigned(string mode)
    {
        (PartitionTestContext ctx, HorseQueue queue) = await CreateSingleQueueServer(mode, "stagger-q", new PartitionOptions
        {
            Enabled = true, MaxPartitionCount = 0, SubscribersPerPartition = 1,
            AutoAssignWorkers = true, MaxPartitionsPerWorker = 0, AutoDestroy = PartitionAutoDestroy.Disabled
        });
        await using PartitionTestContext _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);

        HorseClient w1 = await BuildPoolWorker(ctx.Port, "w1", accessor, "stagger-q");
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);
        for (int i = 0; i < 4; i++)
            await PushLabeled(producer, "stagger-q", $"early-{i}");

        await WaitUntil(() => tracker.TotalCount() >= 4, 5_000);

        HorseClient w2 = await BuildPoolWorker(ctx.Port, "w2", accessor, "stagger-q");
        await Task.Delay(300);

        for (int i = 0; i < 4; i++)
            await PushLabeled(producer, "stagger-q", $"late-{i}");

        await WaitUntil(() => tracker.TotalCount() >= 8, 10_000);

        Assert.Equal(8, queue.PartitionManager.Partitions.Count());

        Dictionary<string, int> assignmentCounts = new();
        foreach (PartitionEntry p in queue.PartitionManager.Partitions)
        foreach (QueueClient qc in p.Queue.Clients)
        {
            string id = qc.Client.UniqueId;
            assignmentCounts[id] = assignmentCounts.GetValueOrDefault(id, 0) + 1;
        }

        Assert.Equal(2, assignmentCounts.Count);
        int w2Count = assignmentCounts.Values.Min();
        Assert.True(w2Count >= 2, $"Late worker should have ≥2 partitions, got {w2Count}");

        producer.Disconnect();
        w1.Disconnect();
        w2.Disconnect();
    }

    #endregion
   

    // ═══════════════════════════════════════════════════════════════════════
    // 19. MaxPartitionCount Override via Attribute
    // ═══════════════════════════════════════════════════════════════════════

    #region MaxPartitionCount Attribute Override

    /// <summary>
    /// Server default MaxPartitionCount=3. PartitionedQueueAttribute on model sets MaxPartitions=1.
    /// Auto-created queue should have MaxPartitionCount=1.
    /// 1st label push → partition created (ok).
    /// 2nd label push → partition creation blocked (MaxPartitionCount reached).
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AttributeOverride_MaxPartitions1_SecondLabelBlocked(string mode)
    {
        // Server default: partitions enabled with MaxPartitionCount=3
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.JustRequest;
            opts.AutoQueueCreation = true;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 3,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 10,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });
        await using var _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);

        // Worker subscribes via DI with LimitedPartitionConsumer
        // PartitionedQueueAttribute(MaxPartitions=1) on the model class
        // → SubscribePartitioned header sends Partition-Limit=1
        // → Queue auto-created with MaxPartitionCount=1
        ServiceCollection services = new();
        services.AddSingleton(accessor);
        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{ctx.Port}");
        builder.SetClientType("limited-worker");
        builder.AutoSubscribe(true);
        builder.AddScopedConsumer<LimitedPartitionConsumer>("label-a", maxPartitions: 1, subscribersPerPartition: 1);

        HorseClient worker = builder.Build();
        await worker.ConnectAsync();
        await Task.Delay(500);

        // "LimitedQ" should now be auto-created with MaxPartitionCount=1
        HorseQueue queue = ctx.Rider.Queue.Find("LimitedQ");
        Assert.NotNull(queue);
        Assert.True(queue.IsPartitioned);
        Assert.Equal(1, queue.Options.Partition.MaxPartitionCount);

        // 1 partition exists (label-a)
        Assert.Single(queue.PartitionManager.Partitions);

        // Push with a new label → should be blocked (MaxPartitionCount=1 reached)
        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        HorseResult result = await bus.Push("LimitedQ",
            new LimitedPartitionModel { Data = "test" },
            waitForCommit: true,
            messageHeaders: null,
            partitionLabel: "label-b",
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(HorseResultCode.Ok, result.Code);

        // Still only 1 partition
        Assert.Single(queue.PartitionManager.Partitions);

        producer.Disconnect();
        worker.Disconnect();
    }

    /// <summary>
    /// Server default MaxPartitionCount=3. PartitionedQueueAttribute on model sets MaxPartitions=0 (unlimited).
    /// MaxPartitions=0 means "unlimited" → Partition-Limit=0 header IS sent.
    /// Queue auto-created with MaxPartitionCount=0 (unlimited).
    /// More than 3 labels should all create partitions.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AttributeOverride_MaxPartitions0_Unlimited_MoreThan3Allowed(string mode)
    {
        // Server default: partitions enabled with MaxPartitionCount=3
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.JustRequest;
            opts.AutoQueueCreation = true;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 3,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 10,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });
        await using var _ = ctx;

        ConsumeTracker tracker = new();
        TrackerAccessor accessor = new(tracker);

        // Worker subscribes with MaxPartitions=0 → header sent as "0" → server creates with unlimited
        ServiceCollection services = new();
        services.AddSingleton(accessor);
        HorseClientBuilder builder = new(services);
        builder.AddHost($"horse://localhost:{ctx.Port}");
        builder.SetClientType("unlimited-worker");
        builder.AutoSubscribe(true);
        builder.AddScopedConsumer<UnlimitedPartitionConsumer>("label-1", maxPartitions: 0, subscribersPerPartition: 1);

        HorseClient worker = builder.Build();
        await worker.ConnectAsync();
        await Task.Delay(500);

        // "UnlimitedQ" should be auto-created with MaxPartitionCount=0 (unlimited)
        HorseQueue queue = ctx.Rider.Queue.Find("UnlimitedQ");
        Assert.NotNull(queue);
        Assert.True(queue.IsPartitioned);
        Assert.Equal(0, queue.Options.Partition.MaxPartitionCount);

        // Push 4 more labels (total 5 including label-1 from subscribe)
        HorseClient producer = await CreateProducer(ctx.Port);
        IHorseQueueBus bus = new HorseQueueBus(producer);
        await bus.Push("UnlimitedQ", new UnlimitedPartitionModel { Data = "t2" }, false, null, "label-2", CancellationToken.None);
        await bus.Push("UnlimitedQ", new UnlimitedPartitionModel { Data = "t3" }, false, null, "label-3", CancellationToken.None);
        await bus.Push("UnlimitedQ", new UnlimitedPartitionModel { Data = "t4" }, false, null, "label-4", CancellationToken.None);
        await bus.Push("UnlimitedQ", new UnlimitedPartitionModel { Data = "t5" }, false, null, "label-5", CancellationToken.None);
        await Task.Delay(500);

        // All 5 partitions should exist (no limit since MaxPartitionCount=0)
        Assert.Equal(5, queue.PartitionManager.Partitions.Count());

        producer.Disconnect();
        worker.Disconnect();
    }

    /// <summary>
    /// Server default MaxPartitionCount=3. Queue created via server API with MaxPartitionCount=0 (unlimited).
    /// 4+ labels should all create partitions without limit.
    /// </summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ServerAPI_MaxPartitionCount0_UnlimitedPartitions(string mode)
    {
        PartitionTestContext ctx = await PartitionTestServer.Create(mode, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.JustRequest;
            opts.AutoQueueCreation = true;
        });
        await using var _ = ctx;

        // Create queue explicitly via server API with MaxPartitionCount=0 (unlimited)
        await ctx.Rider.Queue.Create("Unlimited-Direct-Q", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0, // unlimited
                SubscribersPerPartition = 1,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 10,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = ctx.Rider.Queue.Find("Unlimited-Direct-Q");
        Assert.NotNull(queue);

        // Worker: subscribe to pool via raw API
        HorseClient worker = new HorseClient();
        worker.AutoAcknowledge = true;
        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await worker.ConnectAsync($"horse://localhost:{ctx.Port}");
        await worker.Queue.Subscribe("Unlimited-Direct-Q", true, CancellationToken.None);
        await Task.Delay(300);

        HorseClient producer = await CreateProducer(ctx.Port);

        // Push 5 different labels — all should succeed (no limit)
        for (int i = 1; i <= 5; i++)
        {
            await producer.Queue.Push("Unlimited-Direct-Q",
                System.Text.Encoding.UTF8.GetBytes($"msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"label-{i}") }, CancellationToken.None);
        }

        await WaitUntil(() => queue.PartitionManager.Partitions.Count() >= 5, 5_000);
        Assert.Equal(5, queue.PartitionManager.Partitions.Count());

        // All messages consumed
        await WaitUntil(() => received >= 5, 5_000);
        Assert.True(received >= 5, $"Expected ≥5, got {received}");

        producer.Disconnect();
        worker.Disconnect();
    }

    #endregion
}

