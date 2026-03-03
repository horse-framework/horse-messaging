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
/// Tests for AutoAssignWorkers and MaxPartitionsPerWorker features.
/// Covers: pool registration, on-demand assignment, multi-partition workers,
/// worker recycling via AutoDestroy, capacity limits, message delivery,
/// disconnect cleanup, and AutoDestroy compatibility matrix.
/// </summary>
public class PartitionAutoAssignTest
{
    #region Helpers

    private static async Task<(HorseRider rider, int port, HorseQueue queue)> CreateAutoAssignQueue(
        string name = "aa-q",
        int maxPartitions = 0,
        int subscribersPerPartition = 1,
        int maxPartitionsPerWorker = 1,
        PartitionAutoDestroy autoDestroy = PartitionAutoDestroy.Disabled,
        int autoDestroyIdleSeconds = 2,
        QueueAckDecision ack = QueueAckDecision.None)
    {
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = ack;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = maxPartitionsPerWorker,
                AutoDestroy = autoDestroy,
                AutoDestroyIdleSeconds = autoDestroyIdleSeconds
            };
        });

        HorseQueue queue = rider.Queue.Find(name);
        return (rider, port, queue);
    }

    private static async Task<HorseClient> ConnectWorker(int port)
    {
        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        return client;
    }

    private static async Task SubscribeNoLabel(HorseClient client, string queue = "aa-q")
    {
        await client.Queue.Subscribe(queue, true);
    }

    private static async Task PushLabeled(HorseClient producer, string label, string queue = "aa-q")
    {
        await producer.Queue.Push(queue, Encoding.UTF8.GetBytes($"msg-{label}"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });
    }

    /// <summary>
    /// Polls a condition with 100ms intervals until it returns true or timeout expires.
    /// Eliminates flakiness from fixed Task.Delay waits in timing-sensitive tests.
    /// </summary>
    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        int elapsed = 0;
        while (!condition() && elapsed < timeoutMs)
        {
            await Task.Delay(100);
            elapsed += 100;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Pool Registration
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_NoLabel_AutoAssign_WorkerEntersPool()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue();

        HorseClient worker = await ConnectWorker(port);
        HorseResult result = await worker.Queue.Subscribe("aa-q", true);

        Assert.Equal(HorseResultCode.Ok, result.Code);

        // No partitions should exist yet — worker is in the pool
        await Task.Delay(200);
        Assert.Empty(queue.PartitionManager.Partitions);
    }

    [Fact]
    public async Task Subscribe_NoLabel_AutoAssign_MultipleWorkersEnterPool()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue();

        var workers = new List<HorseClient>();
        for (int i = 0; i < 5; i++)
        {
            HorseClient w = await ConnectWorker(port);
            await SubscribeNoLabel(w);
            workers.Add(w);
        }

        await Task.Delay(200);

        // No partitions — all 5 workers are in the pool
        Assert.Empty(queue.PartitionManager.Partitions);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. On-Demand Assignment (MaxPartitionsPerWorker=1)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Push_Labeled_AutoAssigns_PooledWorker_ToPartition()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue();

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        // Push labeled message — partition created, worker auto-assigned
        await PushLabeled(producer, "tenant-1");
        await Task.Delay(600);

        Assert.Single(queue.PartitionManager.Partitions);
        PartitionEntry entry = queue.PartitionManager.Partitions.First();
        Assert.Equal("tenant-1", entry.Label);
        Assert.True(entry.Queue.Clients.Any());
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Push_TwoLabels_MaxPerWorker1_AssignsTwoSeparateWorkers()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 1);

        HorseClient w1 = await ConnectWorker(port);
        HorseClient w2 = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        await SubscribeNoLabel(w1);
        await SubscribeNoLabel(w2);
        await Task.Delay(200);

        await PushLabeled(producer, "t-A");
        await Task.Delay(300);
        await PushLabeled(producer, "t-B");
        await Task.Delay(300);

        var partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(2, partitions.Count);

        // Each partition has exactly 1 consumer, and they should be different workers
        var consumers = partitions.SelectMany(p => p.Queue.Clients.Select(c => c.Client.UniqueId)).ToList();
        Assert.Equal(2, consumers.Distinct().Count());
    }

    [Fact]
    public async Task Push_ThreeLabels_TwoWorkers_MaxPerWorker1_ThirdPartitionHasNoConsumer()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 1);

        HorseClient w1 = await ConnectWorker(port);
        HorseClient w2 = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);
        Assert.True(producer.IsConnected);

        await SubscribeNoLabel(w1);
        await SubscribeNoLabel(w2);
        await Task.Delay(500);

        await PushLabeled(producer, "t-1");
        await Task.Delay(500);
        await PushLabeled(producer, "t-2");
        await Task.Delay(500);
        await PushLabeled(producer, "t-3");
        await Task.Delay(500);

        var partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(3, partitions.Count);

        // 2 workers used, 3rd partition has no consumer (pool exhausted)
        int withConsumers = partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(2, withConsumers);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Multi-Partition Worker (MaxPartitionsPerWorker > 1)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Push_ThreeLabels_OneWorker_MaxPerWorker3_AllAssignedToSameWorker()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 3);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        await PushLabeled(producer, "a");
        await Task.Delay(200);
        await PushLabeled(producer, "b");
        await Task.Delay(200);
        await PushLabeled(producer, "c");
        await Task.Delay(600);

        var partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(3, partitions.Count);

        // All 3 partitions should have the same worker
        var consumerIds = partitions
            .SelectMany(p => p.Queue.Clients.Select(c => c.Client.UniqueId))
            .Distinct()
            .ToList();
        Assert.Single(consumerIds);
        Assert.Equal(3, received);
    }

    [Fact]
    public async Task MaxPerWorker_Respected_FourthLabel_UsesSecondWorker()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 3);

        HorseClient w1 = await ConnectWorker(port);
        HorseClient w2 = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        await SubscribeNoLabel(w1);
        await SubscribeNoLabel(w2);
        await Task.Delay(200);

        // Push 4 labels — first 3 go to w1, 4th should go to w2
        for (int i = 1; i <= 4; i++)
        {
            await PushLabeled(producer, $"label-{i}");
            await Task.Delay(200);
        }

        await Task.Delay(400);

        var partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(4, partitions.Count);

        // Count distinct consumers
        var consumerIds = partitions
            .Where(p => p.Queue.Clients.Any())
            .SelectMany(p => p.Queue.Clients.Select(c => c.Client.UniqueId))
            .Distinct()
            .ToList();
        Assert.Equal(2, consumerIds.Count);
    }

    [Fact]
    public async Task MaxPerWorker_Zero_Unlimited_SingleWorkerServesAll()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 0);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        // Push 10 different labels
        for (int i = 0; i < 10; i++)
        {
            await PushLabeled(producer, $"tenant-{i}");
            await Task.Delay(100);
        }

        await Task.Delay(800);

        Assert.Equal(10, queue.PartitionManager.Partitions.Count());
        Assert.Equal(10, received);

        // All 10 partitions have the same single worker
        var consumerIds = queue.PartitionManager.Partitions
            .SelectMany(p => p.Queue.Clients.Select(c => c.Client.UniqueId))
            .Distinct()
            .ToList();
        Assert.Single(consumerIds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Message Delivery to Auto-Assigned Workers
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoAssigned_Worker_ReceivesMessagesFromMultiplePartitions()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 5);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        List<string> receivedBodies = new();
        worker.MessageReceived += (_, msg) =>
        {
            string body = Encoding.UTF8.GetString(msg.Content.ToArray());
            lock (receivedBodies)
                receivedBodies.Add(body);
        };

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        await PushLabeled(producer, "x");
        await PushLabeled(producer, "y");
        await PushLabeled(producer, "z");
        await Task.Delay(800);

        Assert.Equal(3, receivedBodies.Count);
        Assert.Contains("msg-x", receivedBodies);
        Assert.Contains("msg-y", receivedBodies);
        Assert.Contains("msg-z", receivedBodies);
    }

    [Fact]
    public async Task SameLabel_MultipleMessages_AllGoToSamePartition()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 5);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        // Push 5 messages all with the same label
        for (int i = 0; i < 5; i++)
        {
            await PushLabeled(producer, "same-tenant");
            await Task.Delay(100);
        }

        await Task.Delay(600);

        // Only 1 partition
        Assert.Single(queue.PartitionManager.Partitions);
        Assert.Equal(5, received);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Worker Recycling via AutoDestroy = NoMessages
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoDestroy_NoMessages_WorkerRecycled_AfterPartitionConsumed()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(
            maxPartitionsPerWorker: 1,
            autoDestroy: PartitionAutoDestroy.NoMessages,
            autoDestroyIdleSeconds: 2);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(300);

        // Push to label "t1" — worker gets assigned
        await PushLabeled(producer, "t1");
        await WaitUntil(() => received >= 1, 3000);
        Assert.Equal(1, received);

        // Wait for NoMessages destroy (timer runs every 2s, needs IsEmpty check)
        await WaitUntil(() => !queue.PartitionManager.Partitions.Any(), 8000);

        // Partition should be destroyed — worker recycled back to pool
        Assert.Empty(queue.PartitionManager.Partitions);

        // Push to label "t2" — worker should be re-assigned from pool
        await PushLabeled(producer, "t2");
        await WaitUntil(() => received >= 2, 3000);
        Assert.Equal(2, received);

        await WaitUntil(() => queue.PartitionManager.Partitions.Any(), 3000);
        Assert.Single(queue.PartitionManager.Partitions);
        PartitionEntry entry = queue.PartitionManager.Partitions.First();
        Assert.Equal("t2", entry.Label);
    }

    [Fact]
    public async Task AutoDestroy_NoMessages_MultiPartitionWorker_CapacityFreed()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(
            maxPartitionsPerWorker: 2,
            autoDestroy: PartitionAutoDestroy.NoMessages,
            autoDestroyIdleSeconds: 5);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);
        Assert.True(worker.IsConnected);
        Assert.True(producer.IsConnected);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(500);

        // Fill both capacity slots
        await PushLabeled(producer, "t1");
        await Task.Delay(500);
        await PushLabeled(producer, "t2");
        await Task.Delay(500);
        Assert.Equal(2, received);
        Assert.Equal(2, queue.PartitionManager.Partitions.Count());

        // Wait for NoMessages to destroy both (idle=5s, check every 5s → ~10s max)
        await Task.Delay(12000);
        Assert.Empty(queue.PartitionManager.Partitions);

        // Worker should have capacity again — push 2 new labels
        await PushLabeled(producer, "t3");
        await Task.Delay(500);
        await PushLabeled(producer, "t4");
        await Task.Delay(500);

        Assert.Equal(4, received);
        Assert.Equal(2, queue.PartitionManager.Partitions.Count());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. AutoDestroy Compatibility Matrix
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoDestroy_Disabled_WorkerNeverRecycled()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(
            maxPartitionsPerWorker: 1,
            autoDestroy: PartitionAutoDestroy.Disabled);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        await PushLabeled(producer, "t1");
        await Task.Delay(600);

        // Partition exists, message consumed, but no destroy
        Assert.Single(queue.PartitionManager.Partitions);

        // Push another label — pool is empty (worker stuck in t1)
        await PushLabeled(producer, "t2");
        await Task.Delay(600);

        // 2 partitions but only first has consumer
        var partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(2, partitions.Count);

        int withConsumer = partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(1, withConsumer);
    }

    [Fact]
    public async Task AutoDestroy_NoConsumers_NotTriggered_WhileWorkerConnected()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(
            maxPartitionsPerWorker: 1,
            autoDestroy: PartitionAutoDestroy.NoConsumers,
            autoDestroyIdleSeconds: 1);

        HorseClient worker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        await SubscribeNoLabel(worker);
        await Task.Delay(200);

        await PushLabeled(producer, "t1");
        await Task.Delay(600);

        // Worker is connected → NoConsumers won't fire
        await Task.Delay(2500);
        Assert.Single(queue.PartitionManager.Partitions);

        // Still only 1 partition, no recycling — second label gets no worker
        await PushLabeled(producer, "t2");
        await Task.Delay(600);

        var partitions = queue.PartitionManager.Partitions.ToList();
        Assert.Equal(2, partitions.Count);
        int withConsumer = partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(1, withConsumer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Worker Disconnect — Pool Cleanup
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectedWorker_SkippedDuringAssignment()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 1);

        HorseClient w1 = await ConnectWorker(port);
        HorseClient w2 = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        await SubscribeNoLabel(w1);
        await SubscribeNoLabel(w2);
        await Task.Delay(200);

        // Disconnect w1 before any assignment
        w1.Disconnect();
        await Task.Delay(200);

        // Push a labeled message — should skip disconnected w1 and assign w2
        int w2Received = 0;
        w2.MessageReceived += (_, _) => Interlocked.Increment(ref w2Received);

        await PushLabeled(producer, "t1");
        await Task.Delay(600);

        Assert.Equal(1, w2Received);
        Assert.Single(queue.PartitionManager.Partitions);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. TryAssignToExistingPartition — Worker arrives after partition exists
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_Arrives_AfterPartitionCreated_AssignedToExisting()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 3);

        HorseClient producer = await ConnectWorker(port);

        // Push messages first — partitions created but no consumers
        await PushLabeled(producer, "t1");
        await PushLabeled(producer, "t2");
        await Task.Delay(300);

        Assert.Equal(2, queue.PartitionManager.Partitions.Count());
        // No consumers yet
        Assert.True(queue.PartitionManager.Partitions.All(p => !p.Queue.Clients.Any()));

        // Now worker subscribes — should be assigned to existing partitions
        HorseClient worker = await ConnectWorker(port);
        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(800);

        // Worker should be assigned to both existing partitions
        int withConsumer = queue.PartitionManager.Partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(2, withConsumer);

        // Messages should be delivered after assignment triggered
        Assert.Equal(2, received);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Tenant Isolation — Messages never cross labels
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MultiTenant_AutoAssign_MessagesStayIsolated()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 1);

        HorseClient wA = await ConnectWorker(port);
        HorseClient wB = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        List<string> wAMessages = new();
        List<string> wBMessages = new();

        wA.MessageReceived += (_, msg) =>
        {
            lock (wAMessages)
                wAMessages.Add(Encoding.UTF8.GetString(msg.Content.ToArray()));
        };
        wB.MessageReceived += (_, msg) =>
        {
            lock (wBMessages)
                wBMessages.Add(Encoding.UTF8.GetString(msg.Content.ToArray()));
        };

        await SubscribeNoLabel(wA);
        await SubscribeNoLabel(wB);
        await Task.Delay(200);

        // Push alternating labels
        await PushLabeled(producer, "A");
        await Task.Delay(200);
        await PushLabeled(producer, "B");
        await Task.Delay(200);
        await PushLabeled(producer, "A");
        await Task.Delay(200);
        await PushLabeled(producer, "B");
        await Task.Delay(600);

        // One worker got all A messages, the other got all B messages
        Assert.Equal(2, wAMessages.Count + wBMessages.Count > 0 ? 2 : 0);

        var allMessages = wAMessages.Concat(wBMessages).ToList();
        Assert.Equal(4, allMessages.Count);

        // Verify each worker only got one label
        if (wAMessages.Count > 0)
            Assert.All(wAMessages, m => Assert.Equal(wAMessages[0], m));
        if (wBMessages.Count > 0)
            Assert.All(wBMessages, m => Assert.Equal(wBMessages[0], m));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 10. Pool Response — Partition-Id: pool
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_AutoAssign_ReturnsOk_WhenPooled()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue();

        HorseClient worker = await ConnectWorker(port);
        HorseResult result = await worker.Queue.Subscribe("aa-q", true);

        // Should succeed with Ok (pooled)
        Assert.Equal(HorseResultCode.Ok, result.Code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 11. AutoAssign OFF — Falls back to standard label-less behavior
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoAssign_Off_NoLabel_CreatesOwnPartition()
    {
        // Create a non-AutoAssign queue for comparison
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create("normal-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = false // ← off
            };
        });

        HorseQueue queue = rider.Queue.Find("normal-q");
        HorseClient client = await ConnectWorker(port);

        await client.Queue.Subscribe("normal-q", true);
        await Task.Delay(200);

        // Standard behavior: label-less subscribe creates its own partition
        Assert.Single(queue.PartitionManager.Partitions);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 12. Mixed: Labeled + AutoAssign workers on same queue
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LabeledWorker_And_AutoAssignWorker_Coexist()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 5);

        HorseClient labeledWorker = await ConnectWorker(port);
        HorseClient autoWorker = await ConnectWorker(port);
        HorseClient producer = await ConnectWorker(port);

        int labeledReceived = 0;
        int autoReceived = 0;

        labeledWorker.MessageReceived += (_, _) => Interlocked.Increment(ref labeledReceived);
        autoWorker.MessageReceived += (_, _) => Interlocked.Increment(ref autoReceived);

        // labeledWorker subscribes with explicit label
        await labeledWorker.Queue.Subscribe("aa-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "vip") });

        // autoWorker subscribes without label → enters pool
        await SubscribeNoLabel(autoWorker);
        await Task.Delay(200);

        // Push to "vip" — goes to labeledWorker
        await PushLabeled(producer, "vip");
        await Task.Delay(300);

        // Push to "other" — auto-assigned to autoWorker from pool
        await PushLabeled(producer, "other");
        await Task.Delay(300);

        Assert.Equal(1, labeledReceived);
        Assert.Equal(1, autoReceived);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 13. Edge: No workers in pool, message stored in partition
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Push_NoWorkersInPool_MessageStoredInPartition()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 1);

        HorseClient producer = await ConnectWorker(port);

        // Push without any worker subscribed
        await PushLabeled(producer, "lonely");
        await Task.Delay(300);

        // Partition created, message stored, but no consumer
        Assert.Single(queue.PartitionManager.Partitions);
        PartitionEntry entry = queue.PartitionManager.Partitions.First();
        Assert.False(entry.Queue.Clients.Any());

        int msgCount = entry.Queue.Manager.MessageStore.Count();
        Assert.Equal(1, msgCount);
    }

    [Fact]
    public async Task Push_NoWorkers_ThenWorkerArrives_MessageDelivered()
    {
        var (rider, port, queue) = await CreateAutoAssignQueue(maxPartitionsPerWorker: 3);

        HorseClient producer = await ConnectWorker(port);

        // Push first — no consumers
        await PushLabeled(producer, "late-tenant");
        await Task.Delay(300);
        Assert.Single(queue.PartitionManager.Partitions);

        // Worker arrives
        HorseClient worker = await ConnectWorker(port);
        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker);
        await Task.Delay(800);

        // TryAssignToExistingPartition should place worker, Trigger delivers message
        Assert.Equal(1, received);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 14. Capacity: MaxPartitions limit still applies
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MaxPartitions_Limit_StillApplied_ToLabellessSubscribe()
    {
        // MaxPartitionCount applies to label-less subscribes.
        // Labeled pushes always create partitions for routing (by design).
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create("limit-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 2,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = false // standard mode to test MaxPartitionCount
            };
        });

        HorseQueue queue = rider.Queue.Find("limit-q");

        HorseClient c1 = await ConnectWorker(port);
        HorseClient c2 = await ConnectWorker(port);
        HorseClient c3 = await ConnectWorker(port);

        HorseResult r1 = await c1.Queue.Subscribe("limit-q", true);
        HorseResult r2 = await c2.Queue.Subscribe("limit-q", true);
        Assert.Equal(HorseResultCode.Ok, r1.Code);
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        // 3rd label-less subscribe → max reached → LimitExceeded
        HorseResult r3 = await c3.Queue.Subscribe("limit-q", true);
        Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);

        Assert.Equal(2, queue.PartitionManager.Partitions.Count());
    }
}

