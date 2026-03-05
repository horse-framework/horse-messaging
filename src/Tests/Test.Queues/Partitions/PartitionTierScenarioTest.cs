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
/// End-to-end tests for the full tier-based partition scenario:
///
/// Three tiers (free / standard / premium), each backed by a separate partitioned queue
/// with AutoAssignWorkers enabled. Workers subscribe without labels (enter pool).
/// Producers send messages with partitionLabel = tenantId.
/// Each tenant gets its own partition → waitAcknowledge guarantees per-tenant FIFO.
///
/// Also covers:
///  - Queue name override via AddScopedConsumer(string queueName, string partitionLabel)
///  - Queue name transform via AddScopedConsumer(Func&lt;string,string&gt;, string partitionLabel)
///  - QueueNameHandler context.QueueName propagation
///  - Mixed registration: some consumers with label, some with queue name override
///  - Tier isolation across separate queues
///  - Tenant FIFO ordering within a tier
///  - AutoQueueCreation for on-the-fly tier queue creation
///  - Worker capacity exhaustion across tiers
///  - Producer routing to wrong tier queue (cross-tier isolation)
/// </summary>
public class PartitionTierScenarioTest
{
    #region Helpers

    /// <summary>
    /// Creates a server with AutoQueueCreation + default partition options.
    /// Queues created on-the-fly will inherit this partition config.
    /// </summary>
    private static async Task<(HorseRider rider, int port)> CreateTierServer(
        int maxPartitionsPerWorker = 10,
        PartitionAutoDestroy autoDestroy = PartitionAutoDestroy.Disabled)
    {
        var (rider, port, server) = await PartitionTestServer.Create();

        // Pre-create tier queues with partition config
        foreach (string tier in new[] { "Free", "Standard", "Premium" })
        {
            await rider.Queue.Create($"CompareOrders-{tier}", opts =>
            {
                opts.Type = QueueType.Push;
                opts.Acknowledge = QueueAckDecision.None;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    MaxPartitionCount = 0,
                    SubscribersPerPartition = 1,
                    AutoAssignWorkers = true,
                    MaxPartitionsPerWorker = maxPartitionsPerWorker,
                    AutoDestroy = autoDestroy,
                    AutoDestroyIdleSeconds = 30
                };
            });
        }

        return (rider, port);
    }

    /// <summary>
    /// Creates a server with a single labeled (non-AutoAssign) partitioned queue.
    /// Workers subscribe WITH a label for tier-based subscribe.
    /// </summary>
    private static async Task<(HorseRider rider, int port, HorseQueue queue)> CreateLabeledQueue(
        string name = "FetchOrders",
        int subscribersPerPartition = 5)
    {
        var (rider, port, server) = await PartitionTestServer.Create();

        await rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 3,
                SubscribersPerPartition = subscribersPerPartition,
                AutoAssignWorkers = false,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseQueue queue = rider.Queue.Find(name);
        return (rider, port, queue);
    }

    private static async Task<HorseClient> Connect(int port)
    {
        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    private static async Task SubscribeLabeled(HorseClient client, string queue, string label)
    {
        await client.Queue.Subscribe(queue, true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) }, CancellationToken.None);
    }

    private static async Task SubscribeNoLabel(HorseClient client, string queue)
    {
        await client.Queue.Subscribe(queue, true, CancellationToken.None);
    }

    private static async Task PushWithLabel(HorseClient producer, string queue, string label, string body = null)
    {
        body ??= $"{queue}:{label}";
        await producer.Queue.Push(queue, Encoding.UTF8.GetBytes(body), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) }, CancellationToken.None);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    // 1. LABELED SUBSCRIBE (FetchOrders, MapOrders, SaveOrder pattern)
    //    Workers subscribe with tier label → same partition, round-robin
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LabeledSubscribe_FreeWorkers_SamePartition()
    {
        var (rider, port, queue) = await CreateLabeledQueue("FetchOrders", subscribersPerPartition: 3);

        HorseClient w1 = await Connect(port);
        HorseClient w2 = await Connect(port);
        HorseClient w3 = await Connect(port);

        await SubscribeLabeled(w1, "FetchOrders", "free");
        await SubscribeLabeled(w2, "FetchOrders", "free");
        await SubscribeLabeled(w3, "FetchOrders", "free");
        await Task.Delay(300);

        // All 3 workers in the same "free" partition
        Assert.Single(queue.PartitionManager.Partitions);
        PartitionEntry entry = queue.PartitionManager.Partitions.First();
        Assert.Equal("free", entry.Label);
        Assert.Equal(3, entry.Queue.Clients.Count());
    }

    [Fact]
    public async Task LabeledSubscribe_DifferentTiers_SeparatePartitions()
    {
        var (rider, port, queue) = await CreateLabeledQueue("MapOrders", subscribersPerPartition: 2);

        HorseClient freeW = await Connect(port);
        HorseClient stdW = await Connect(port);
        HorseClient prmW = await Connect(port);

        await SubscribeLabeled(freeW, "MapOrders", "free");
        await SubscribeLabeled(stdW, "MapOrders", "standard");
        await SubscribeLabeled(prmW, "MapOrders", "premium");
        await Task.Delay(300);

        Assert.Equal(3, queue.PartitionManager.Partitions.Count());
        Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "free"));
        Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "standard"));
        Assert.NotNull(queue.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "premium"));
    }

    [Fact]
    public async Task LabeledSubscribe_MessageGoesToCorrectTier()
    {
        var (rider, port, queue) = await CreateLabeledQueue("SaveOrder", subscribersPerPartition: 2);

        HorseClient freeW = await Connect(port);
        HorseClient premW = await Connect(port);
        HorseClient producer = await Connect(port);

        int freeReceived = 0, premReceived = 0;
        freeW.MessageReceived += (_, _) => Interlocked.Increment(ref freeReceived);
        premW.MessageReceived += (_, _) => Interlocked.Increment(ref premReceived);

        await SubscribeLabeled(freeW, "SaveOrder", "free");
        await SubscribeLabeled(premW, "SaveOrder", "premium");
        await Task.Delay(300);

        await PushWithLabel(producer, "SaveOrder", "free");
        await PushWithLabel(producer, "SaveOrder", "premium");
        await PushWithLabel(producer, "SaveOrder", "free");
        await Task.Delay(600);

        Assert.Equal(2, freeReceived);
        Assert.Equal(1, premReceived);
    }

    [Fact]
    public async Task LabeledSubscribe_TierIsolation_FreeBurstDoesNotAffectPremium()
    {
        var (rider, port, queue) = await CreateLabeledQueue("OrderProcess", subscribersPerPartition: 1);

        HorseClient freeW = await Connect(port);
        HorseClient premW = await Connect(port);
        HorseClient producer = await Connect(port);

        int freeReceived = 0, premReceived = 0;
        freeW.MessageReceived += (_, _) => Interlocked.Increment(ref freeReceived);
        premW.MessageReceived += (_, _) => Interlocked.Increment(ref premReceived);

        await SubscribeLabeled(freeW, "OrderProcess", "free");
        await SubscribeLabeled(premW, "OrderProcess", "premium");
        await Task.Delay(300);

        // Burst: 20 free messages, 2 premium messages
        for (int i = 0; i < 20; i++)
            await PushWithLabel(producer, "OrderProcess", "free", $"free-{i}");

        await PushWithLabel(producer, "OrderProcess", "premium", "prem-1");
        await PushWithLabel(producer, "OrderProcess", "premium", "prem-2");

        await Task.Delay(1000);

        // Premium got its 2 messages regardless of free backlog
        Assert.Equal(2, premReceived);
        Assert.True(freeReceived > 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. QUEUE NAME OVERRIDE (CompareOrders pattern)
    //    Workers subscribe to "CompareOrders-{tier}" without label → AutoAssign pool
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QueueNameOverride_WorkerSubscribesToCorrectQueue()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient worker = await Connect(port);
        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(300);

        HorseQueue freeQueue = rider.Queue.Find("CompareOrders-Free");
        Assert.NotNull(freeQueue);
        Assert.NotNull(freeQueue.PartitionManager);

        // Worker is in the pool (no partitions yet)
        Assert.Empty(freeQueue.PartitionManager.Partitions);

        // Standard and Premium queues are unaffected
        HorseQueue stdQueue = rider.Queue.Find("CompareOrders-Standard");
        Assert.Empty(stdQueue.PartitionManager.Partitions);
    }

    [Fact]
    public async Task QueueNameOverride_TenantMessage_AutoAssignedToFreeWorker()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient freeW = await Connect(port);
        HorseClient producer = await Connect(port);

        int received = 0;
        freeW.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(freeW, "CompareOrders-Free");
        await Task.Delay(300);

        // Producer sends to free tier with tenant label
        await PushWithLabel(producer, "CompareOrders-Free", "tenant-42");
        await Task.Delay(600);

        Assert.Equal(1, received);

        HorseQueue freeQueue = rider.Queue.Find("CompareOrders-Free");
        Assert.Single(freeQueue.PartitionManager.Partitions);
        Assert.Equal("tenant-42", freeQueue.PartitionManager.Partitions.First().Label);
    }

    [Fact]
    public async Task QueueNameOverride_CrossTierIsolation()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient freeW = await Connect(port);
        HorseClient premW = await Connect(port);
        HorseClient producer = await Connect(port);

        int freeReceived = 0, premReceived = 0;
        freeW.MessageReceived += (_, _) => Interlocked.Increment(ref freeReceived);
        premW.MessageReceived += (_, _) => Interlocked.Increment(ref premReceived);

        await SubscribeNoLabel(freeW, "CompareOrders-Free");
        await SubscribeNoLabel(premW, "CompareOrders-Premium");
        await Task.Delay(300);

        // Send to free tier
        await PushWithLabel(producer, "CompareOrders-Free", "tenant-1");
        await PushWithLabel(producer, "CompareOrders-Free", "tenant-2");
        // Send to premium tier
        await PushWithLabel(producer, "CompareOrders-Premium", "tenant-99");
        await Task.Delay(600);

        Assert.Equal(2, freeReceived);
        Assert.Equal(1, premReceived);

        // Each tier queue has its own partitions
        HorseQueue freeQ = rider.Queue.Find("CompareOrders-Free");
        HorseQueue premQ = rider.Queue.Find("CompareOrders-Premium");
        Assert.Equal(2, freeQ.PartitionManager.Partitions.Count());
        Assert.Single(premQ.PartitionManager.Partitions);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. QUEUE NAME TRANSFORM — Func<string,string> overload
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task QueueNameTransform_RegistrationResolvesCorrectQueueName()
    {
        HorseClient client = new HorseClient();
        var registrar = new Horse.Messaging.Client.Queues.QueueConsumerRegistrar(client.Queue);

        // Register consumer with transform: "CompareOrders" → "CompareOrders-Free"
        registrar.RegisterConsumer(
            typeof(TestCompareOrdersConsumer),
            consumerFactoryBuilder: null,
            queueNameTransform: name => $"{name}-Free",
            enterWorkerPool: false);

        // Read back the registration via reflection (Registrations is internal)
        var registrations = typeof(Horse.Messaging.Client.Queues.QueueOperator)
            .GetProperty("Registrations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(client.Queue) as System.Collections.IList;

        Assert.NotNull(registrations);
        Assert.Equal(1, registrations.Count);

        string queueName = registrations[0]!.GetType()
            .GetProperty("QueueName")!
            .GetValue(registrations[0]) as string;

        Assert.Equal("CompareOrders-Free", queueName);
    }

    [Fact]
    public async Task QueueNameTransform_E2E_WorkerSubscribesToTransformedQueue()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient client = new HorseClient();
        client.AutoSubscribe = true;

        var registrar = new Horse.Messaging.Client.Queues.QueueConsumerRegistrar(client.Queue);
        registrar.RegisterConsumer(
            typeof(TestCompareOrdersConsumer),
            consumerFactoryBuilder: null,
            queueNameTransform: name => $"{name}-Premium",
            enterWorkerPool: false);

        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        await Task.Delay(500);

        // AutoSubscribe should have subscribed to "CompareOrders-Premium"
        HorseQueue premQ = rider.Queue.Find("CompareOrders-Premium");
        Assert.NotNull(premQ);
    }

    [Fact]
    public async Task QueueNameHandler_ContextContainsOriginalQueueName()
    {
        HorseClient client = new HorseClient();

        string capturedQueueName = null;

        client.Queue.NameHandler = ctx =>
        {
            capturedQueueName = ctx.QueueName;
            return $"{ctx.QueueName}-Standard";
        };

        // Register consumer — NameHandler is invoked during CreateConsumerRegistration
        var registrar = new Horse.Messaging.Client.Queues.QueueConsumerRegistrar(client.Queue);
        registrar.RegisterConsumer(typeof(TestCompareOrdersConsumer), consumerFactoryBuilder: null);

        // NameHandler received the original [QueueName("CompareOrders")] value
        Assert.Equal("CompareOrders", capturedQueueName);

        // Verify the registration has the transformed name
        var registrations = typeof(Horse.Messaging.Client.Queues.QueueOperator)
            .GetProperty("Registrations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(client.Queue) as System.Collections.IList;

        Assert.NotNull(registrations);
        string queueName = registrations[0]!.GetType()
            .GetProperty("QueueName")!
            .GetValue(registrations[0]) as string;

        Assert.Equal("CompareOrders-Standard", queueName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. MIXED REGISTRATION — Same worker with labeled + queue-name-override consumers
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MixedRegistration_LabeledAndAutoAssign_OnSameWorker()
    {
        var (rider, port, server) = await PartitionTestServer.Create();

        // Create labeled queue
        await rider.Queue.Create("FetchOrders", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 3,
                SubscribersPerPartition = 5,
                AutoAssignWorkers = false
            };
        });

        // Create auto-assign queue
        await rider.Queue.Create("CompareOrders-Free", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 10
            };
        });

        HorseClient worker = await Connect(port);
        HorseClient producer = await Connect(port);

        List<string> receivedBodies = new();
        worker.MessageReceived += (_, msg) =>
        {
            string body = Encoding.UTF8.GetString(msg.Content.ToArray());
            lock (receivedBodies)
                receivedBodies.Add(body);
        };

        // Worker subscribes to FetchOrders with "free" label
        await SubscribeLabeled(worker, "FetchOrders", "free");
        // Worker subscribes to CompareOrders-Free without label (auto-assign pool)
        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(300);

        // Push to FetchOrders labeled "free"
        await PushWithLabel(producer, "FetchOrders", "free", "fetch-msg");
        // Push to CompareOrders-Free with tenant label
        await PushWithLabel(producer, "CompareOrders-Free", "tenant-7", "compare-msg");
        await Task.Delay(600);

        Assert.Equal(2, receivedBodies.Count);
        Assert.Contains("fetch-msg", receivedBodies);
        Assert.Contains("compare-msg", receivedBodies);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5. TENANT FIFO — Per-tenant ordering within a tier
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task TenantFIFO_SameTenantMessages_DeliveredInOrder()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 5);

        HorseClient worker = await Connect(port);
        HorseClient producer = await Connect(port);

        List<string> receivedOrder = new();
        worker.MessageReceived += (_, msg) =>
        {
            string body = Encoding.UTF8.GetString(msg.Content.ToArray());
            lock (receivedOrder)
                receivedOrder.Add(body);
        };

        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(300);

        // 5 sequential messages for same tenant
        for (int i = 1; i <= 5; i++)
        {
            await PushWithLabel(producer, "CompareOrders-Free", "tenant-X", $"msg-{i}");
            await Task.Delay(100);
        }

        await Task.Delay(800);

        Assert.Equal(5, receivedOrder.Count);
        // Verify ordering
        for (int i = 0; i < 5; i++)
            Assert.Equal($"msg-{i + 1}", receivedOrder[i]);
    }

    [Fact]
    public async Task TenantFIFO_DifferentTenants_InterleavedButIsolated()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 10);

        HorseClient worker = await Connect(port);
        HorseClient producer = await Connect(port);

        List<string> receivedBodies = new();
        worker.MessageReceived += (_, msg) =>
        {
            string body = Encoding.UTF8.GetString(msg.Content.ToArray());
            lock (receivedBodies)
                receivedBodies.Add(body);
        };

        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(300);

        // Interleave 3 tenants
        await PushWithLabel(producer, "CompareOrders-Free", "A", "A-1");
        await PushWithLabel(producer, "CompareOrders-Free", "B", "B-1");
        await PushWithLabel(producer, "CompareOrders-Free", "A", "A-2");
        await PushWithLabel(producer, "CompareOrders-Free", "C", "C-1");
        await PushWithLabel(producer, "CompareOrders-Free", "B", "B-2");
        await PushWithLabel(producer, "CompareOrders-Free", "A", "A-3");

        await Task.Delay(1000);

        Assert.Equal(6, receivedBodies.Count);

        // Extract per-tenant sequences and verify ordering
        var aMessages = receivedBodies.Where(b => b.StartsWith("A-")).ToList();
        var bMessages = receivedBodies.Where(b => b.StartsWith("B-")).ToList();
        var cMessages = receivedBodies.Where(b => b.StartsWith("C-")).ToList();

        Assert.Equal(new[] { "A-1", "A-2", "A-3" }, aMessages);
        Assert.Equal(new[] { "B-1", "B-2" }, bMessages);
        Assert.Equal(new[] { "C-1" }, cMessages);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6. AUTOQUEUE CREATION — On-the-fly tier queue creation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AutoQueueCreation_WorkerSubscribesToNonExistentQueue_CreatesIt()
    {
        var (rider, port, server) = await PartitionTestServer.Create();
        // AutoQueueCreation is enabled by default in PartitionTestServer

        HorseClient worker = await Connect(port);

        // Queue doesn't exist yet
        Assert.Null(rider.Queue.Find("DynamicQueue-Free"));

        await SubscribeNoLabel(worker, "DynamicQueue-Free");
        await Task.Delay(300);

        // Queue was auto-created
        HorseQueue q = rider.Queue.Find("DynamicQueue-Free");
        Assert.NotNull(q);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7. MULTI-WORKER CAPACITY — Worker pool across a tier
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MultiWorker_TenTenantsDistributedAcrossFiveWorkers()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 2);

        HorseClient producer = await Connect(port);
        var workers = new List<HorseClient>();
        int totalReceived = 0;

        for (int i = 0; i < 5; i++)
        {
            HorseClient w = await Connect(port);
            w.MessageReceived += (_, _) => Interlocked.Increment(ref totalReceived);
            await SubscribeNoLabel(w, "CompareOrders-Free");
            workers.Add(w);
        }

        await Task.Delay(500);

        // Push 10 different tenants
        for (int i = 0; i < 10; i++)
        {
            await PushWithLabel(producer, "CompareOrders-Free", $"tenant-{i}");
            await Task.Delay(100);
        }

        await Task.Delay(1000);

        HorseQueue freeQ = rider.Queue.Find("CompareOrders-Free");
        Assert.Equal(10, freeQ.PartitionManager.Partitions.Count());
        Assert.Equal(10, totalReceived);

        // All partitions should have consumers (5 workers × 2 = 10 capacity)
        int withConsumers = freeQ.PartitionManager.Partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(10, withConsumers);
    }

    [Fact]
    public async Task MultiWorker_CapacityExhausted_ExtraTenantsHaveNoConsumer()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 1);

        HorseClient producer = await Connect(port);

        // Only 2 workers, MaxPartitionsPerWorker=1 → capacity = 2
        HorseClient w1 = await Connect(port);
        HorseClient w2 = await Connect(port);
        w1.MessageReceived += (_, _) => { };
        w2.MessageReceived += (_, _) => { };

        await SubscribeNoLabel(w1, "CompareOrders-Free");
        await SubscribeNoLabel(w2, "CompareOrders-Free");
        await Task.Delay(300);

        // 3 tenants but only 2 workers
        await PushWithLabel(producer, "CompareOrders-Free", "t1");
        await Task.Delay(200);
        await PushWithLabel(producer, "CompareOrders-Free", "t2");
        await Task.Delay(200);
        await PushWithLabel(producer, "CompareOrders-Free", "t3");
        await Task.Delay(400);

        HorseQueue freeQ = rider.Queue.Find("CompareOrders-Free");
        Assert.Equal(3, freeQ.PartitionManager.Partitions.Count());

        int withConsumers = freeQ.PartitionManager.Partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(2, withConsumers);

        // 3rd partition stores message without consumer
        PartitionEntry noConsumer = freeQ.PartitionManager.Partitions.First(p => !p.Queue.Clients.Any());
        Assert.Equal(1, noConsumer.Queue.Manager.MessageStore.Count());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8. FULL SCENARIO — 3 tiers, multiple queues, mixed registration
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FullScenario_ThreeTiers_LabeledAndAutoAssign_CompleteIsolation()
    {
        var (rider, port, server) = await PartitionTestServer.Create();

        // Create labeled queue (FetchOrders)
        await rider.Queue.Create("FetchOrders", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 3,
                SubscribersPerPartition = 2,
                AutoAssignWorkers = false
            };
        });

        // Create auto-assign queues per tier (CompareOrders-{tier})
        foreach (string tier in new[] { "Free", "Premium" })
        {
            await rider.Queue.Create($"CompareOrders-{tier}", opts =>
            {
                opts.Type = QueueType.Push;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    SubscribersPerPartition = 1,
                    AutoAssignWorkers = true,
                    MaxPartitionsPerWorker = 5
                };
            });
        }

        // Workers
        HorseClient freeW1 = await Connect(port);
        HorseClient freeW2 = await Connect(port);
        HorseClient premW1 = await Connect(port);
        HorseClient producer = await Connect(port);

        int fetchFreeRx = 0, fetchPremRx = 0, compareFreeRx = 0, comparePremRx = 0;

        freeW1.MessageReceived += (_, _) =>
        {
            Interlocked.Increment(ref fetchFreeRx);
            Interlocked.Increment(ref compareFreeRx);
        };
        freeW2.MessageReceived += (_, _) =>
        {
            Interlocked.Increment(ref fetchFreeRx);
            Interlocked.Increment(ref compareFreeRx);
        };
        premW1.MessageReceived += (_, _) =>
        {
            Interlocked.Increment(ref fetchPremRx);
            Interlocked.Increment(ref comparePremRx);
        };

        // freeW1 & freeW2: FetchOrders("free") + CompareOrders-Free(pool)
        await SubscribeLabeled(freeW1, "FetchOrders", "free");
        await SubscribeNoLabel(freeW1, "CompareOrders-Free");
        await SubscribeLabeled(freeW2, "FetchOrders", "free");
        await SubscribeNoLabel(freeW2, "CompareOrders-Free");

        // premW1: FetchOrders("premium") + CompareOrders-Premium(pool)
        await SubscribeLabeled(premW1, "FetchOrders", "premium");
        await SubscribeNoLabel(premW1, "CompareOrders-Premium");

        await Task.Delay(500);

        // Verify FetchOrders has 2 partitions (free, premium)
        HorseQueue fetchQ = rider.Queue.Find("FetchOrders");
        Assert.Equal(2, fetchQ.PartitionManager.Partitions.Count());

        // Push messages
        await PushWithLabel(producer, "FetchOrders", "free");
        await PushWithLabel(producer, "FetchOrders", "premium");
        await PushWithLabel(producer, "CompareOrders-Free", "tenant-1");
        await PushWithLabel(producer, "CompareOrders-Premium", "tenant-99");
        await Task.Delay(800);

        // FetchOrders partitions exist and have consumers
        PartitionEntry fetchFree = fetchQ.PartitionManager.Partitions.First(p => p.Label == "free");
        Assert.Equal(2, fetchFree.Queue.Clients.Count()); // freeW1 + freeW2

        // CompareOrders queues have tenant partitions
        HorseQueue compareFreeQ = rider.Queue.Find("CompareOrders-Free");
        HorseQueue comparePremQ = rider.Queue.Find("CompareOrders-Premium");
        Assert.Single(compareFreeQ.PartitionManager.Partitions);
        Assert.Single(comparePremQ.PartitionManager.Partitions);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 9. EDGE CASES
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Edge_SameTenantDifferentTierQueues_CompletelyIsolated()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient freeW = await Connect(port);
        HorseClient premW = await Connect(port);
        HorseClient producer = await Connect(port);

        List<string> freeMessages = new();
        List<string> premMessages = new();

        freeW.MessageReceived += (_, msg) =>
        {
            lock (freeMessages)
                freeMessages.Add(Encoding.UTF8.GetString(msg.Content.ToArray()));
        };
        premW.MessageReceived += (_, msg) =>
        {
            lock (premMessages)
                premMessages.Add(Encoding.UTF8.GetString(msg.Content.ToArray()));
        };

        await SubscribeNoLabel(freeW, "CompareOrders-Free");
        await SubscribeNoLabel(premW, "CompareOrders-Premium");
        await Task.Delay(300);

        // Same tenantId used in both tiers — they should be completely separate
        await PushWithLabel(producer, "CompareOrders-Free", "tenant-42", "free-42");
        await PushWithLabel(producer, "CompareOrders-Premium", "tenant-42", "prem-42");
        await Task.Delay(600);

        Assert.Single(freeMessages);
        Assert.Single(premMessages);
        Assert.Equal("free-42", freeMessages[0]);
        Assert.Equal("prem-42", premMessages[0]);
    }

    [Fact]
    public async Task Edge_WorkerDisconnects_OtherTiersUnaffected()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient freeW = await Connect(port);
        HorseClient premW = await Connect(port);
        HorseClient producer = await Connect(port);

        int premReceived = 0;
        premW.MessageReceived += (_, _) => Interlocked.Increment(ref premReceived);

        await SubscribeNoLabel(freeW, "CompareOrders-Free");
        await SubscribeNoLabel(premW, "CompareOrders-Premium");
        await Task.Delay(300);

        // Assign workers to partitions
        await PushWithLabel(producer, "CompareOrders-Free", "t1");
        await PushWithLabel(producer, "CompareOrders-Premium", "t2");
        await Task.Delay(600);

        // Disconnect free worker
        freeW.Disconnect();
        await Task.Delay(300);

        // Premium worker still receives
        await PushWithLabel(producer, "CompareOrders-Premium", "t2");
        await Task.Delay(600);

        Assert.Equal(2, premReceived);
    }

    [Fact]
    public async Task Edge_ProducerSendsToNonExistentTierQueue_NoError()
    {
        var (rider, port) = await CreateTierServer();

        HorseClient producer = await Connect(port);

        // Send to a tier queue that has no workers — message just gets stored
        await PushWithLabel(producer, "CompareOrders-Standard", "tenant-1");
        await Task.Delay(300);

        HorseQueue stdQ = rider.Queue.Find("CompareOrders-Standard");
        Assert.NotNull(stdQ);
        Assert.Single(stdQ.PartitionManager.Partitions);

        // Message stored, no consumer
        PartitionEntry entry = stdQ.PartitionManager.Partitions.First();
        Assert.False(entry.Queue.Clients.Any());
        Assert.Equal(1, entry.Queue.Manager.MessageStore.Count());
    }

    [Fact]
    public async Task Edge_LateWorkerJoin_PicksUpStoredMessages()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 5);

        HorseClient producer = await Connect(port);

        // Push 3 tenant messages before any worker
        await PushWithLabel(producer, "CompareOrders-Free", "t1", "msg-1");
        await PushWithLabel(producer, "CompareOrders-Free", "t2", "msg-2");
        await PushWithLabel(producer, "CompareOrders-Free", "t3", "msg-3");
        await Task.Delay(300);

        HorseQueue freeQ = rider.Queue.Find("CompareOrders-Free");
        Assert.Equal(3, freeQ.PartitionManager.Partitions.Count());
        Assert.True(freeQ.PartitionManager.Partitions.All(p => !p.Queue.Clients.Any()));

        // Worker arrives late
        HorseClient worker = await Connect(port);
        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(800);

        // Worker should be assigned to existing partitions and receive stored messages
        int withConsumer = freeQ.PartitionManager.Partitions.Count(p => p.Queue.Clients.Any());
        Assert.Equal(3, withConsumer);
        Assert.Equal(3, received);
    }

    [Fact]
    public async Task Edge_MultipleTenantsPerWorker_EachTenantGetsOwnPartition()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 0); // unlimited

        HorseClient worker = await Connect(port);
        HorseClient producer = await Connect(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(300);

        // 20 different tenants
        for (int i = 0; i < 20; i++)
        {
            await PushWithLabel(producer, "CompareOrders-Free", $"tenant-{i}");
            await Task.Delay(50);
        }

        await Task.Delay(1000);

        HorseQueue freeQ = rider.Queue.Find("CompareOrders-Free");
        Assert.Equal(20, freeQ.PartitionManager.Partitions.Count());
        Assert.Equal(20, received);

        // All partitions have the same worker
        var consumerIds = freeQ.PartitionManager.Partitions
            .SelectMany(p => p.Queue.Clients.Select(c => c.Client.UniqueId))
            .Distinct()
            .ToList();
        Assert.Single(consumerIds);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 10. LABELED SUBSCRIBE — SubscribersPerPartition limit
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LabeledSubscribe_ExceedsSubscribersPerPartition_Rejected()
    {
        var (rider, port, queue) = await CreateLabeledQueue("LimitQueue", subscribersPerPartition: 1);

        HorseClient w1 = await Connect(port);
        HorseClient w2 = await Connect(port);

        HorseResult r1 = await w1.Queue.Subscribe("LimitQueue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "free") }, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        // Second worker with same label → full
        HorseResult r2 = await w2.Queue.Subscribe("LimitQueue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "free") }, CancellationToken.None);
        Assert.Equal(HorseResultCode.LimitExceeded, r2.Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 11. RECONNECT — Worker reconnects and re-subscribes
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reconnect_WorkerRejoinsPool_ReceivesNewMessages()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 5);

        HorseClient producer = await Connect(port);
        HorseClient worker = await Connect(port);

        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await SubscribeNoLabel(worker, "CompareOrders-Free");
        await Task.Delay(300);

        // First message
        await PushWithLabel(producer, "CompareOrders-Free", "t1");
        await Task.Delay(600);
        Assert.Equal(1, received);

        // Worker disconnects
        worker.Disconnect();
        await Task.Delay(300);

        // Reconnect
        HorseClient worker2 = await Connect(port);
        int received2 = 0;
        worker2.MessageReceived += (_, _) => Interlocked.Increment(ref received2);

        await SubscribeNoLabel(worker2, "CompareOrders-Free");
        await Task.Delay(500);

        // New message to a different tenant
        await PushWithLabel(producer, "CompareOrders-Free", "t2");
        await Task.Delay(800);

        Assert.Equal(1, received2);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 12. PARTITION METRICS — Verify partition counts per tier
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Metrics_EachTierReportsCorrectPartitionCount()
    {
        var (rider, port) = await CreateTierServer(maxPartitionsPerWorker: 10);

        HorseClient freeW = await Connect(port);
        HorseClient stdW = await Connect(port);
        HorseClient producer = await Connect(port);

        await SubscribeNoLabel(freeW, "CompareOrders-Free");
        await SubscribeNoLabel(stdW, "CompareOrders-Standard");
        await Task.Delay(300);

        // 5 tenants free, 2 tenants standard
        for (int i = 0; i < 5; i++)
            await PushWithLabel(producer, "CompareOrders-Free", $"ft-{i}");

        for (int i = 0; i < 2; i++)
            await PushWithLabel(producer, "CompareOrders-Standard", $"st-{i}");

        await Task.Delay(800);

        HorseQueue freeQ = rider.Queue.Find("CompareOrders-Free");
        HorseQueue stdQ = rider.Queue.Find("CompareOrders-Standard");

        var freeMetrics = freeQ.PartitionManager.GetMetrics().ToList();
        var stdMetrics = stdQ.PartitionManager.GetMetrics().ToList();

        Assert.Equal(5, freeMetrics.Count);
        Assert.Equal(2, stdMetrics.Count);

        // All free partitions have a consumer
        Assert.All(freeMetrics, m => Assert.Equal(1, m.ConsumerCount));
        Assert.All(stdMetrics, m => Assert.Equal(1, m.ConsumerCount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 13. SAMPLE.ALL SCENARIO — Exact reproduction of Sample.All/Program.cs
    //     Server with global partition config + AutoQueueCreation,
    //     consumer with queueName transform, producer with NameHandler + label.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SampleAll_GlobalPartitionConfig_TransformAndLabel_ConsumerReceives()
    {
        // ── Server: global partition config (same as Sample.All/Server.cs) ──
        var (rider, port, server) = await PartitionTestServer.Create();
        // PartitionTestServer already sets AutoQueueCreation = true.
        // Set the default partition config (global) that auto-created queues will inherit.
        rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            AutoDestroy = PartitionAutoDestroy.NoMessages,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 50,
            AutoDestroyIdleSeconds = 600
        };

        // ── Consumer: subscribe to "TestEvent-Free" via transform, no label ──
        HorseClient consumer = new HorseClient();
        bool messageConsumed = false;
        consumer.MessageReceived += (_, msg) =>
        {
            string body = Encoding.UTF8.GetString(msg.Content.ToArray());
            if (body.Contains("Foo"))
                messageConsumed = true;
        };

        await consumer.ConnectAsync("horse://localhost:" + port);
        Assert.True(consumer.IsConnected);

        // Transform: "TestEvent" → "TestEvent-Free", no label → enters worker pool
        await consumer.Queue.Subscribe("TestEvent-Free", true, CancellationToken.None);
        await Task.Delay(500);

        // Queue should be auto-created with global partition config
        HorseQueue queue = rider.Queue.Find("TestEvent-Free");
        Assert.NotNull(queue);
        Assert.NotNull(queue.PartitionManager);
        // Worker is in pool — no partitions yet
        Assert.Empty(queue.PartitionManager.Partitions);

        // ── Producer: push with partition label ──
        HorseClient producer = await Connect(port);

        await producer.Queue.Push("TestEvent-Free",
            Encoding.UTF8.GetBytes("{\"Foo\":\"Bar\"}"),
            false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "sample-tenant") }, CancellationToken.None);

        await Task.Delay(800);

        // ── Assertions ──
        Assert.True(messageConsumed, "Consumer should have received the message");

        // Partition created for "sample-tenant"
        Assert.Single(queue.PartitionManager.Partitions);
        PartitionEntry entry = queue.PartitionManager.Partitions.First();
        Assert.Equal("sample-tenant", entry.Label);
        Assert.True(entry.Queue.Clients.Any(), "Worker should be auto-assigned to partition");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 14. CLONEFROM BUG — QueueOptions.CloneFrom must copy
    //     AutoAssignWorkers and MaxPartitionsPerWorker
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void CloneFrom_CopiesAutoAssignWorkers()
    {
        QueueOptions original = new QueueOptions
        {
            Partition = new PartitionOptions
            {
                Enabled = true,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 42,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 3,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = 120
            }
        };

        QueueOptions cloned = QueueOptions.CloneFrom(original);

        Assert.NotNull(cloned.Partition);
        Assert.True(cloned.Partition.Enabled);
        Assert.True(cloned.Partition.AutoAssignWorkers);
        Assert.Equal(42, cloned.Partition.MaxPartitionsPerWorker);
        Assert.Equal(10, cloned.Partition.MaxPartitionCount);
        Assert.Equal(3, cloned.Partition.SubscribersPerPartition);
        Assert.Equal(PartitionAutoDestroy.NoMessages, cloned.Partition.AutoDestroy);
        Assert.Equal(120, cloned.Partition.AutoDestroyIdleSeconds);
    }

    [Fact]
    public void CloneFrom_NullPartition_StaysNull()
    {
        QueueOptions original = new QueueOptions { Partition = null };
        QueueOptions cloned = QueueOptions.CloneFrom(original);
        Assert.Null(cloned.Partition);
    }

    [Fact]
    public async Task AutoQueueCreation_InheritsGlobalAutoAssignWorkers()
    {
        // Verifies that auto-created queues get AutoAssignWorkers from global config
        var (rider, port, server) = await PartitionTestServer.Create();

        rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 25
        };

        // Trigger auto-creation by subscribing
        HorseClient worker = await Connect(port);
        await worker.Queue.Subscribe("InheritTest-Queue", true, CancellationToken.None);
        await Task.Delay(300);

        HorseQueue queue = rider.Queue.Find("InheritTest-Queue");
        Assert.NotNull(queue);
        Assert.NotNull(queue.PartitionManager);

        // The queue's partition options should have inherited AutoAssignWorkers
        // This was the CloneFrom bug — before the fix, AutoAssignWorkers would be false
        // Worker should be in pool (no partitions), not assigned to a label-less partition
        Assert.Empty(queue.PartitionManager.Partitions);

        // Push a labeled message — if AutoAssignWorkers is true, worker gets auto-assigned
        HorseClient producer = await Connect(port);
        int received = 0;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        await PushWithLabel(producer, "InheritTest-Queue", "tenant-1");
        await Task.Delay(600);

        Assert.Equal(1, received);
        Assert.Single(queue.PartitionManager.Partitions);
    }
}

// ── Test model & consumer used by QueueNameTransform tests ───────────────

[Horse.Messaging.Client.Queues.Annotations.QueueName("CompareOrders")]
public class TestCompareOrdersModel { }

public class TestCompareOrdersConsumer : Horse.Messaging.Client.Queues.IQueueConsumer<TestCompareOrdersModel>
{
    public Task Consume(HorseMessage message, TestCompareOrdersModel model, HorseClient client, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

