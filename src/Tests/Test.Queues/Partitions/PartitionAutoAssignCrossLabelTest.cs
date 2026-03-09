using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Test.Queues.Partitions;

/// <summary>
/// Regression test for the AutoAssignWorkers cross-label contamination bug.
///
/// Real-world scenario from Parrot production logs (2026-03-08):
///   Config: AutoAssignWorkers=true, AutoDestroy=NoMessages,
///           SubscribersPerPartition=1, MaxPartitionsPerWorker=1.
///
///   1. EVENT_CLIENT_FREE subscribes with label "free"  → Partition-free  created (1/1)
///      EVENT_CLIENT_STANDARD subscribes with label "standard" → Partition-standard created (1/1)
///   2. No messages flow for AutoDestroyIdleSeconds.
///      AutoDestroy=NoMessages timer fires → partitions are destroyed.
///      OnPartitionQueueDestroyed puts connected clients into the worker pool
///      WITHOUT preserving their original label.
///      Clients are still connected (no disconnect).
///   3. A message is pushed with label "standard" → RouteMessage creates new Partition-standard.
///      New partition has 0 clients → RouteMessage calls TryAssignPooledWorker.
///      TryAssignPooledWorker picks the FIRST worker from the pool — which may be
///      EVENT_CLIENT_FREE — and assigns it to Partition-standard ← CROSS LABEL BUG!
///   4. Result: EVENT_CLIENT_FREE ends up in Partition-standard ❌
///
/// The key insight: labeled subscribe path never touches the pool, but
/// RouteMessage (message push) DOES call TryAssignPooledWorker on ANY
/// partition that has fewer clients than SubscribersPerPartition, regardless
/// of the worker's original label.
/// </summary>
public class PartitionAutoAssignCrossLabelTest
{
    private readonly ITestOutputHelper _output;

    public PartitionAutoAssignCrossLabelTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private static async Task<(TestHorseRider server, int port, HorseQueue queue)> CreateAutoAssignQueue(
        string name = "EventQueue",
        int autoDestroyIdleSeconds = 2)
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 1,
                SubscribersPerPartition = 1,
                AutoDestroyIdleSeconds = autoDestroyIdleSeconds
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find(name);
        return (server, port, queue);
    }

    private static async Task<HorseClient> ConnectOnly(int port)
    {
        HorseClient client = new HorseClient();
        client.AutoSubscribe = false;
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    private static async Task SubscribeWithLabel(HorseClient client, string queue, string label)
    {
        HorseResult result = await client.Queue.Subscribe(queue, true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) },
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);
    }

    /// <summary>
    /// Returns the set of partition labels that a given client is subscribed to
    /// within a partitioned parent queue.
    /// </summary>
    private static HashSet<string> GetClientPartitionLabels(HorseQueue parentQueue, string clientId)
    {
        var labels = new HashSet<string>();

        foreach (PartitionEntry entry in parentQueue.PartitionManager.Partitions)
        {
            bool isSubscribed = entry.Queue.Clients.Any(c => c.Client.UniqueId == clientId);
            if (isSubscribed && entry.Label != null)
                labels.Add(entry.Label);
        }

        return labels;
    }

    /// <summary>
    /// Reproduces the cross-label contamination via message push (RouteMessage path).
    ///
    /// Phase 1: Two clients subscribe with different labels → correct partitions.
    /// Phase 2: Partitions are destroyed by NoMessages auto-destroy.
    ///          Clients stay connected → pool gets both workers (no label info).
    /// Phase 3: A message is pushed to the parent queue with label "standard".
    ///          RouteMessage → GetOrCreateLabelPartition("standard") → new partition (0 clients).
    ///          RouteMessage sees Clients.Count()==0 &lt; SubscribersPerPartition==1 →
    ///          calls TryAssignPooledWorker → picks EVENT_CLIENT_FREE from pool →
    ///          assigns to Partition-standard. ← CROSS LABEL!
    /// Phase 4: Verify EVENT_CLIENT_FREE is NOT in Partition-standard.
    ///
    /// This test MUST FAIL to prove the bug exists.
    /// </summary>
    [Fact]
    public async Task AutoAssign_MessagePush_AfterDestroy_CrossLabel()
    {
        var (server, port, queue) = await CreateAutoAssignQueue("EventQueue", autoDestroyIdleSeconds: 2);

        // ─── Phase 1: Two clients subscribe with labels ─────────────────
        HorseClient eventFree = await ConnectOnly(port);
        HorseClient eventStandard = await ConnectOnly(port);

        await SubscribeWithLabel(eventFree, "EventQueue", "free");
        await SubscribeWithLabel(eventStandard, "EventQueue", "standard");

        await Task.Delay(500);

        string freeId = eventFree.ClientId;
        string standardId = eventStandard.ClientId;

        // Verify: 2 partitions, each client in correct partition
        Assert.Equal(2, queue.PartitionManager.Partitions.Count());
        Assert.Single(GetClientPartitionLabels(queue, freeId));
        Assert.Contains("free", GetClientPartitionLabels(queue, freeId));
        Assert.Single(GetClientPartitionLabels(queue, standardId));
        Assert.Contains("standard", GetClientPartitionLabels(queue, standardId));

        _output.WriteLine($"Phase 1 OK: free={freeId}, standard={standardId}");

        // ─── Phase 2: Wait for AutoDestroy=NoMessages ───────────────────
        await Task.Delay(4000);

        Assert.Empty(queue.PartitionManager.Partitions);
        Assert.True(eventFree.IsConnected, "EVENT_CLIENT_FREE should still be connected");
        Assert.True(eventStandard.IsConnected, "EVENT_CLIENT_STANDARD should still be connected");

        _output.WriteLine("Phase 2 OK: All partitions destroyed, clients still connected");


        // ─── Phase 3: Push a message with label "standard" ──────────────
        // This triggers RouteMessage → creates Partition-standard (0 clients)
        // → TryAssignPooledWorker → picks from pool (EVENT_CLIENT_FREE first)
        // → assigns to Partition-standard regardless of label.
        HorseMessage pushMsg = new HorseMessage(MessageType.QueueMessage, "EventQueue");
        pushMsg.SetStringContent("test-payload");
        pushMsg.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
        PushResult pushResult = await queue.Push(pushMsg);

        _output.WriteLine($"Push result: {pushResult}");

        await Task.Delay(1000); // let auto-assign settle

        // ─── Phase 4: Verify label isolation ────────────────────────────
        HashSet<string> eventFreeLabels = GetClientPartitionLabels(queue, freeId);
        HashSet<string> eventStandardLabels = GetClientPartitionLabels(queue, standardId);

        _output.WriteLine($"EVENT_CLIENT_FREE partitions: [{string.Join(", ", eventFreeLabels)}]");
        _output.WriteLine($"EVENT_CLIENT_STANDARD partitions: [{string.Join(", ", eventStandardLabels)}]");

        // EVENT_CLIENT_FREE must NOT be in "standard" partition
        Assert.DoesNotContain("standard", eventFreeLabels);

        // EVENT_CLIENT_STANDARD should be in "standard" (pool auto-assign) — that's OK
        // But EVENT_CLIENT_FREE must NOT be there

        server.Stop();
    }

    /// <summary>
    /// Same scenario but with three labels and multiple message pushes.
    /// Proves that pool workers leak across ALL labels, not just one specific pair.
    /// </summary>
    [Fact]
    public async Task AutoAssign_MessagePush_AfterDestroy_MultiLabel_CrossContamination()
    {
        var (server, port, queue) = await CreateAutoAssignQueue("EventQueue", autoDestroyIdleSeconds: 2);

        // ─── Phase 1: Three clients subscribe with labels ───────────────
        HorseClient eventFree = await ConnectOnly(port);
        HorseClient eventStandard = await ConnectOnly(port);
        HorseClient eventPremium = await ConnectOnly(port);

        await SubscribeWithLabel(eventFree, "EventQueue", "free");
        await SubscribeWithLabel(eventStandard, "EventQueue", "standard");
        await SubscribeWithLabel(eventPremium, "EventQueue", "premium");

        await Task.Delay(500);

        string freeId = eventFree.ClientId;
        string standardId = eventStandard.ClientId;
        string premiumId = eventPremium.ClientId;

        Assert.Equal(3, queue.PartitionManager.Partitions.Count());

        // ─── Phase 2: Wait for AutoDestroy ──────────────────────────────
        await Task.Delay(4000);

        Assert.Empty(queue.PartitionManager.Partitions);
        Assert.True(eventFree.IsConnected);
        Assert.True(eventStandard.IsConnected);
        Assert.True(eventPremium.IsConnected);

        // ─── Phase 3: Push messages with different labels ───────────────
        // Each push creates a new partition and triggers TryAssignPooledWorker

        // Push to "standard" → creates Partition-standard → pool worker assigned (could be eventFree!)
        HorseMessage msg1 = new HorseMessage(MessageType.QueueMessage, "EventQueue");
        msg1.SetStringContent("msg-standard");
        msg1.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
        await queue.Push(msg1);
        await Task.Delay(300);

        // Push to "premium" → creates Partition-premium → pool worker assigned (could be any remaining!)
        HorseMessage msg2 = new HorseMessage(MessageType.QueueMessage, "EventQueue");
        msg2.SetStringContent("msg-premium");
        msg2.AddHeader(HorseHeaders.PARTITION_LABEL, "premium");
        await queue.Push(msg2);
        await Task.Delay(300);

        // Push to "free" → creates Partition-free → pool worker assigned
        HorseMessage msg3 = new HorseMessage(MessageType.QueueMessage, "EventQueue");
        msg3.SetStringContent("msg-free");
        msg3.AddHeader(HorseHeaders.PARTITION_LABEL, "free");
        await queue.Push(msg3);
        await Task.Delay(500);

        // ─── Phase 4: Verify label isolation ────────────────────────────
        HashSet<string> freeLabels = GetClientPartitionLabels(queue, freeId);
        HashSet<string> standardLabels = GetClientPartitionLabels(queue, standardId);
        HashSet<string> premiumLabels = GetClientPartitionLabels(queue, premiumId);

        _output.WriteLine($"EVENT_FREE in: [{string.Join(", ", freeLabels)}]");
        _output.WriteLine($"EVENT_STANDARD in: [{string.Join(", ", standardLabels)}]");
        _output.WriteLine($"EVENT_PREMIUM in: [{string.Join(", ", premiumLabels)}]");

        // Each client must ONLY appear in its own label's partition (or nowhere)
        Assert.DoesNotContain("standard", freeLabels);
        Assert.DoesNotContain("premium", freeLabels);

        Assert.DoesNotContain("free", standardLabels);
        Assert.DoesNotContain("premium", standardLabels);

        Assert.DoesNotContain("free", premiumLabels);
        Assert.DoesNotContain("standard", premiumLabels);

        server.Stop();
    }

    /// <summary>
    /// Dynamic tenant-id partition scenario (Parrot AddScopedTenantEventConsumer pattern).
    ///
    /// Real-world: Queue names are "{Event}-{tier}" (e.g. OrderCreated-free, OrderCreated-standard).
    /// Each tier queue is a separate partitioned parent queue with AutoAssignWorkers=true.
    /// Messages arrive with PARTITION_LABEL = tenant-id (e.g. "tenant-1", "tenant-2").
    /// Workers subscribe to their tier's queue without a label → go to pool.
    /// When a message with tenant-id arrives, a partition is created and a pooled worker is assigned.
    ///
    /// Expected:
    ///   - Worker-free can serve tenant-1 partition, finish, return to pool, then serve tenant-2.
    ///     (Same tier, different tenant → OK)
    ///   - Worker-free must NOT be assigned to OrderCreated-standard's tenant partitions.
    ///     (Different tier → cross-tier isolation via separate PartitionManager instances)
    ///   - Within a single tier queue, after partition destroy + re-create,
    ///     the worker must be re-assigned to the SAME tier's new tenant partition (not leak cross-tier).
    ///
    /// This test uses TWO separate parent queues (one per tier) to match the Parrot pattern,
    /// and verifies that each queue's pool is isolated. A third "cross-check" queue proves
    /// that workers from one tier's pool do NOT appear in another tier's partitions after
    /// destroy/re-create cycles.
    ///
    /// Additionally, it verifies that within a single tier queue, a worker can float
    /// between multiple tenant partitions as they are created by message push.
    ///
    /// This test MUST FAIL on the cross-label contamination bug (pool ignores labels).
    /// </summary>
    [Fact]
    public async Task AutoAssign_TenantPartitions_WorkerFloatsSameTier_NoCrossTierLeak()
    {
        var server = new TestHorseRider();
        await server.Initialize();

        // Create two tier-specific queues (Parrot pattern: "{Event}-{tier}")
        // MaxPartitionsPerWorker=0 (unlimited) so worker can serve multiple tenants
        await server.Rider.Queue.Create("OrderCreated-free", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.AutoQueueCreation = true;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 0, // unlimited — worker can serve many tenants
                SubscribersPerPartition = 1,
                AutoDestroyIdleSeconds = 2
            };
        });

        await server.Rider.Queue.Create("OrderCreated-standard", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.AutoQueueCreation = true;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 0,
                SubscribersPerPartition = 1,
                AutoDestroyIdleSeconds = 2
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queueFree = server.Rider.Queue.Find("OrderCreated-free");
        HorseQueue queueStandard = server.Rider.Queue.Find("OrderCreated-standard");
        Assert.NotNull(queueFree);
        Assert.NotNull(queueStandard);

        // ─── Phase 1: Workers subscribe to their tier queue (no label → pool) ───
        HorseClient workerFree = await ConnectOnly(port);
        HorseClient workerStandard = await ConnectOnly(port);

        // Subscribe without label → goes to available worker pool of that queue
        HorseResult r1 = await workerFree.Queue.Subscribe("OrderCreated-free", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        HorseResult r2 = await workerStandard.Queue.Subscribe("OrderCreated-standard", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        await Task.Delay(300);

        string freeId = workerFree.ClientId;
        string standardId = workerStandard.ClientId;

        _output.WriteLine($"WorkerFree={freeId}, WorkerStandard={standardId}");

        // ─── Phase 2: Push messages with tenant-id labels → partitions created ───
        // Push tenant-1 to free queue → creates "tenant-1" partition → workerFree assigned
        HorseMessage msgFreeTenant1 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-free");
        msgFreeTenant1.SetStringContent("free-tenant-1-msg");
        msgFreeTenant1.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
        await queueFree.Push(msgFreeTenant1);
        await Task.Delay(300);

        // Push tenant-2 to free queue → creates "tenant-2" partition → workerFree assigned (unlimited)
        HorseMessage msgFreeTenant2 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-free");
        msgFreeTenant2.SetStringContent("free-tenant-2-msg");
        msgFreeTenant2.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-2");
        await queueFree.Push(msgFreeTenant2);
        await Task.Delay(300);

        // Push tenant-1 to standard queue → creates "tenant-1" partition → workerStandard assigned
        HorseMessage msgStdTenant1 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-standard");
        msgStdTenant1.SetStringContent("std-tenant-1-msg");
        msgStdTenant1.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
        await queueStandard.Push(msgStdTenant1);
        await Task.Delay(300);

        // Verify: workerFree is in free queue partitions, NOT in standard queue partitions
        HashSet<string> freeWorkerInFreeQ = GetClientPartitionLabels(queueFree, freeId);
        HashSet<string> freeWorkerInStdQ = GetClientPartitionLabels(queueStandard, freeId);

        HashSet<string> stdWorkerInStdQ = GetClientPartitionLabels(queueStandard, standardId);
        HashSet<string> stdWorkerInFreeQ = GetClientPartitionLabels(queueFree, standardId);

        _output.WriteLine($"WorkerFree in free-queue: [{string.Join(", ", freeWorkerInFreeQ)}]");
        _output.WriteLine($"WorkerFree in std-queue: [{string.Join(", ", freeWorkerInStdQ)}]");
        _output.WriteLine($"WorkerStandard in std-queue: [{string.Join(", ", stdWorkerInStdQ)}]");
        _output.WriteLine($"WorkerStandard in free-queue: [{string.Join(", ", stdWorkerInFreeQ)}]");

        // WorkerFree should be in tenant-1 AND tenant-2 of free queue (multi-tenant OK)
        Assert.Contains("tenant-1", freeWorkerInFreeQ);
        Assert.Contains("tenant-2", freeWorkerInFreeQ);

        // WorkerFree must NOT be in standard queue
        Assert.Empty(freeWorkerInStdQ);

        // WorkerStandard should be in tenant-1 of standard queue
        Assert.Contains("tenant-1", stdWorkerInStdQ);

        // WorkerStandard must NOT be in free queue
        Assert.Empty(stdWorkerInFreeQ);

        _output.WriteLine("Phase 2 OK: Tier isolation verified, multi-tenant within tier OK");

        // ─── Phase 3: Partitions auto-destroy, then new tenants arrive ───
        // Wait for NoMessages auto-destroy
        await Task.Delay(4000);

        Assert.Empty(queueFree.PartitionManager.Partitions);
        Assert.Empty(queueStandard.PartitionManager.Partitions);
        Assert.True(workerFree.IsConnected);
        Assert.True(workerStandard.IsConnected);

        _output.WriteLine("Phase 3: All partitions destroyed, workers still connected");


        // Push tenant-3 to free queue → new partition → workerFree should be assigned
        HorseMessage msgFreeTenant3 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-free");
        msgFreeTenant3.SetStringContent("free-tenant-3-msg");
        msgFreeTenant3.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-3");
        await queueFree.Push(msgFreeTenant3);
        await Task.Delay(200);

        // Verify immediately: workerFree must be in tenant-3 partition of free queue
        // (check before auto-destroy timer can fire again)
        HashSet<string> freeWorkerInFreeQ2 = GetClientPartitionLabels(queueFree, freeId);
        HashSet<string> freeWorkerInStdQ2 = GetClientPartitionLabels(queueStandard, freeId);

        _output.WriteLine($"After push free - WorkerFree in free-queue: [{string.Join(", ", freeWorkerInFreeQ2)}]");
        _output.WriteLine($"After push free - WorkerFree in std-queue: [{string.Join(", ", freeWorkerInStdQ2)}]");

        Assert.Contains("tenant-3", freeWorkerInFreeQ2);
        Assert.Empty(freeWorkerInStdQ2);

        // Push tenant-3 to standard queue → new partition → workerStandard should be assigned
        HorseMessage msgStdTenant3 = new HorseMessage(MessageType.QueueMessage, "OrderCreated-standard");
        msgStdTenant3.SetStringContent("std-tenant-3-msg");
        msgStdTenant3.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-3");
        await queueStandard.Push(msgStdTenant3);
        await Task.Delay(200);

        // Verify immediately: workerStandard must be in tenant-3 partition of standard queue
        HashSet<string> stdWorkerInStdQ2 = GetClientPartitionLabels(queueStandard, standardId);
        HashSet<string> stdWorkerInFreeQ2 = GetClientPartitionLabels(queueFree, standardId);

        _output.WriteLine($"After push std - WorkerStandard in std-queue: [{string.Join(", ", stdWorkerInStdQ2)}]");
        _output.WriteLine($"After push std - WorkerStandard in free-queue: [{string.Join(", ", stdWorkerInFreeQ2)}]");

        Assert.Contains("tenant-3", stdWorkerInStdQ2);
        Assert.Empty(stdWorkerInFreeQ2);

        server.Stop();
    }

    /// <summary>
    /// Combined scenario: worker subscribes to BOTH a tier-level partitioned queue (with label)
    /// AND a tenant-specific partitioned queue (without label, AutoQueueCreation).
    ///
    /// Parrot pattern:
    ///   worker-free subscribes to "FetchOrdersEvent" with label "free"      → tier partition
    ///   worker-free subscribes to "CompareProducts-free" without label       → tenant pool
    ///
    /// When "FetchOrdersEvent" Partition-free is destroyed and re-created by message push,
    /// worker-free must be re-assigned to Partition-free (not Partition-standard).
    ///
    /// Meanwhile, "CompareProducts-free" gets tenant messages → worker-free gets assigned
    /// to tenant partitions within that queue — no cross-tier contamination because
    /// CompareProducts-standard is a different queue with a different PartitionManager.
    ///
    /// This test combines both patterns and verifies:
    /// 1. Tier-level label isolation on the shared event queue
    /// 2. Tenant-level floating within the tier-specific queue
    /// 3. No cross contamination after destroy/re-create cycles
    ///
    /// This test MUST FAIL on the cross-label contamination bug.
    /// </summary>
    [Fact]
    public async Task AutoAssign_CombinedTierAndTenant_LabelIsolation()
    {
        var server = new TestHorseRider();
        await server.Initialize();

        // Shared event queue — tier-level partitioning (label = "free"/"standard")
        await server.Rider.Queue.Create("FetchOrdersEvent", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 1,
                SubscribersPerPartition = 1,
                AutoDestroyIdleSeconds = 2
            };
        });

        // Tenant-specific queues — one per tier
        await server.Rider.Queue.Create("CompareProducts-free", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.AutoQueueCreation = true;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 0, // unlimited tenants
                SubscribersPerPartition = 1,
                AutoDestroyIdleSeconds = 2
            };
        });

        await server.Rider.Queue.Create("CompareProducts-standard", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.AutoQueueCreation = true;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoAssignWorkers = true,
                MaxPartitionCount = 0,
                MaxPartitionsPerWorker = 0,
                SubscribersPerPartition = 1,
                AutoDestroyIdleSeconds = 2
            };
        });

        int port = server.Start(300, 300);

        HorseQueue fetchQueue = server.Rider.Queue.Find("FetchOrdersEvent");
        HorseQueue compareFreeQueue = server.Rider.Queue.Find("CompareProducts-free");
        HorseQueue compareStdQueue = server.Rider.Queue.Find("CompareProducts-standard");

        // ─── Workers connect and subscribe ────────────────────────────────
        HorseClient workerFree = await ConnectOnly(port);
        HorseClient workerStandard = await ConnectOnly(port);

        string freeId = workerFree.ClientId;
        string standardId = workerStandard.ClientId;

        // Tier-level: subscribe to shared event queue WITH label
        await SubscribeWithLabel(workerFree, "FetchOrdersEvent", "free");
        await SubscribeWithLabel(workerStandard, "FetchOrdersEvent", "standard");

        // Tenant-level: subscribe to tier-specific queue WITHOUT label (→ pool)
        HorseResult r1 = await workerFree.Queue.Subscribe("CompareProducts-free", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        HorseResult r2 = await workerStandard.Queue.Subscribe("CompareProducts-standard", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        await Task.Delay(500);

        // Verify initial tier-level partitions
        Assert.Equal(2, fetchQueue.PartitionManager.Partitions.Count());
        Assert.Contains("free", GetClientPartitionLabels(fetchQueue, freeId));
        Assert.Contains("standard", GetClientPartitionLabels(fetchQueue, standardId));

        _output.WriteLine("Initial subscribe OK");

        // ─── Push tenant messages to tier-specific queues ─────────────────
        HorseMessage tenantMsg1 = new HorseMessage(MessageType.QueueMessage, "CompareProducts-free");
        tenantMsg1.SetStringContent("compare-free-t1");
        tenantMsg1.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
        await compareFreeQueue.Push(tenantMsg1);
        await Task.Delay(300);

        HorseMessage tenantMsg2 = new HorseMessage(MessageType.QueueMessage, "CompareProducts-standard");
        tenantMsg2.SetStringContent("compare-std-t1");
        tenantMsg2.AddHeader(HorseHeaders.PARTITION_LABEL, "tenant-1");
        await compareStdQueue.Push(tenantMsg2);
        await Task.Delay(300);

        // Verify tenant partitions — each worker in its own tier's tenant queue
        Assert.Contains("tenant-1", GetClientPartitionLabels(compareFreeQueue, freeId));
        Assert.Empty(GetClientPartitionLabels(compareStdQueue, freeId));
        Assert.Contains("tenant-1", GetClientPartitionLabels(compareStdQueue, standardId));
        Assert.Empty(GetClientPartitionLabels(compareFreeQueue, standardId));

        _output.WriteLine("Tenant partition assignment OK");

        // ─── Wait for auto-destroy on shared event queue ──────────────────
        await Task.Delay(4000);

        Assert.Empty(fetchQueue.PartitionManager.Partitions);
        Assert.True(workerFree.IsConnected);
        Assert.True(workerStandard.IsConnected);

        _output.WriteLine("FetchOrdersEvent partitions destroyed");

        // ─── Push message to shared event queue → triggers TryAssignPooledWorker ──
        HorseMessage eventMsgStd = new HorseMessage(MessageType.QueueMessage, "FetchOrdersEvent");
        eventMsgStd.SetStringContent("fetch-std-payload");
        eventMsgStd.AddHeader(HorseHeaders.PARTITION_LABEL, "standard");
        await fetchQueue.Push(eventMsgStd);
        await Task.Delay(500);

        HorseMessage eventMsgFree = new HorseMessage(MessageType.QueueMessage, "FetchOrdersEvent");
        eventMsgFree.SetStringContent("fetch-free-payload");
        eventMsgFree.AddHeader(HorseHeaders.PARTITION_LABEL, "free");
        await fetchQueue.Push(eventMsgFree);
        await Task.Delay(500);

        // ─── Verify: cross-label contamination must NOT happen ────────────
        HashSet<string> freeInFetch = GetClientPartitionLabels(fetchQueue, freeId);
        HashSet<string> stdInFetch = GetClientPartitionLabels(fetchQueue, standardId);

        _output.WriteLine($"After re-create — WorkerFree in FetchOrdersEvent: [{string.Join(", ", freeInFetch)}]");
        _output.WriteLine($"After re-create — WorkerStandard in FetchOrdersEvent: [{string.Join(", ", stdInFetch)}]");

        // WorkerFree must be in "free" only, NOT "standard"
        Assert.DoesNotContain("standard", freeInFetch);

        // WorkerStandard must be in "standard" only, NOT "free"
        Assert.DoesNotContain("free", stdInFetch);

        server.Stop();
    }
}


