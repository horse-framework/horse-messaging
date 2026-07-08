using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;
using Xunit;
using Xunit.Abstractions;

namespace Test.Nodes;

public class NodeQueueTest
{
    private readonly ITestOutputHelper _output;

    public NodeQueueTest(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Reproduces the production S7-horse NRE deterministically at the exact failing path:
    /// a successor replica queue reaches Status=Running with a null Manager (CreateReplica with
    /// Initialized=false skips init; a stray NodeQueueStateMessage then advances Status via
    /// SetStatus without touching Manager), and a replicated push arrives via HorseQueue.PushByNode.
    ///
    /// Pre-fix:  PushByNode dereferences the null Manager (HorseQueue.cs:1175) → NRE → returns Error,
    ///           the replica silently drops the message.
    /// Post-fix: guards keyed on Manager==null (HorseQueue.cs:364 + :1125) let PushByNode initialize
    ///           the Manager, store the message, and return Success.
    /// </summary>
    [Fact]
    public async Task PushByNode_On_Uninitialized_Running_Replica_Initializes_And_Stores()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q =>
            {
                q.UseMemoryQueues();
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
            })
            .Build();

        // Queue creation/initialization needs the server context (manager factories), so run a
        // single standalone server (no cluster) — CreateReplica/PushByNode are cluster-independent.
        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 28650 }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);

        try
        {
        // Build a valid NodeQueueInfo from a real (initialized) queue, then flip Initialized=false
        // so the replica is created in the pre-init state (Manager stays null).
        HorseQueue source = rider.Queue.Find("SourceTemplate") ?? await rider.Queue.Create("SourceTemplate");
        Assert.NotNull(source);
        NodeQueueInfo info = source.ClusterNotifier.CreateNodeQueueInfo();
        info.Name = "ReplicaBugQueue";
        info.HandlerName = "Default";
        info.Initialized = false;

        // 1. Replica created pre-init → Manager null, Status NotInitialized.
        HorseQueue replica = await rider.Queue.CreateReplica(info);
        Assert.NotNull(replica);
        Assert.Equal(QueueStatus.NotInitialized, replica.Status);
        Assert.Null(replica.Manager);

        // 2. Advance to Running without initializing (stray NodeQueueStateMessage) → BUG STATE.
        replica.SetStatus(QueueStatus.Running);
        Assert.Equal(QueueStatus.Running, replica.Status);
        Assert.Null(replica.Manager);

        // 3. Deliver a replicated push (what NodeClient.PushByNode does on the successor).
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, "ReplicaBugQueue");
        message.SetStringContent("replicated-payload");
        message.CalculateLengths();

        PushResult result = await replica.PushByNode(message);

        _output.WriteLine($"result={result} Manager={(replica.Manager == null ? "NULL" : "set")} Count={replica.Manager?.MessageStore.Count()}");

        // 4. Post-fix expectations.
        Assert.Equal(PushResult.Success, result);
        Assert.NotNull(replica.Manager);
        Assert.Equal(1, replica.Manager.MessageStore.Count());
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
        }
    }

    /// <summary>
    /// Reproduces the SECOND production NRE (eventId 209, "Initialize In Push Queue"):
    /// HorseQueue.Push (the client-publish path) has its OWN init block at HorseQueue.cs:844 whose
    /// guard is keyed on Status only (Status == NotInitialized) — the Manager-null guard from the
    /// PushByNode fix was NOT applied here. A pre-init replica queue (Manager null) that receives a
    /// direct Push goes through this block and NREs before/inside initialization.
    ///
    /// Pre-fix:  Push throws inside the init block -> SendError "Initialize In Push Queue" (:869) -> throw.
    /// Post-fix: the init block initializes the Manager and the message is stored (Success).
    /// </summary>
    [Fact]
    public async Task Push_On_Uninitialized_Replica_Initializes_And_Stores()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q =>
            {
                q.UseMemoryQueues();
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
                // Non-partitioned: this test asserts the message lands in the queue's OWN store. The
                // partition+label routing path is covered by Push_WithDynamicPartitionLabels and the
                // persistent-config 209 repro; a partition-enabled queue with no consumer legitimately
                // returns NoConsumers now that init completes (PartitionManager is created).
            })
            .Build();

        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 28660 }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);

        try
        {
            HorseQueue source = rider.Queue.Find("SourceTemplate2") ?? await rider.Queue.Create("SourceTemplate2");
            Assert.NotNull(source);
            NodeQueueInfo info = source.ClusterNotifier.CreateNodeQueueInfo();
            info.Name = "PushBugQueue";
            info.HandlerName = "Default";
            info.Initialized = false;

            // Replica created pre-init → Manager null, Status NotInitialized. NO SetStatus:
            // Status stays NotInitialized so Push takes its own init block (HorseQueue.cs:844).
            HorseQueue replica = await rider.Queue.CreateReplica(info);
            Assert.NotNull(replica);
            Assert.Equal(QueueStatus.NotInitialized, replica.Status);
            Assert.Null(replica.Manager);

            // Direct client-publish Push (sender null, as a node/system push).
            HorseMessage message = new HorseMessage(MessageType.QueueMessage, "PushBugQueue");
            message.SetStringContent("push-payload");
            message.CalculateLengths();

            PushResult result = await replica.Push(new QueueMessage(message), null);

            _output.WriteLine($"result={result} Status={replica.Status} Manager={(replica.Manager == null ? "NULL" : "set")} Count={replica.Manager?.MessageStore.Count()}");

            Assert.Equal(PushResult.Success, result);
            Assert.NotNull(replica.Manager);
            Assert.Equal(1, replica.Manager.MessageStore.Count());
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
        }
    }

    /// <summary>
    /// DETERMINISTIC reproduce of the Push guard-asymmetry bug — the exact sibling of the proven 204
    /// PushByNode NRE, on the client-publish path. HorseQueue.Push:844 guards ONLY on
    /// Status == NotInitialized, whereas the fixed PushByNode:1133 guards on
    /// (Status == NotInitialized || Manager == null). A replica that reached Status=Running while its
    /// Manager is still null (CreateReplica pre-init + a stray NodeQueueStateMessage advancing Status,
    /// exactly the 204 state) therefore SKIPS Push's init block and dereferences the null Manager.
    ///
    /// Pre-fix:  Push returns Error / throws NullReferenceException (Manager is null, init skipped).
    /// Post-fix: adding `|| Manager == null` to :844 lets Push initialize the Manager and store (Success).
    /// </summary>
    [Fact]
    public async Task Push_On_Running_Replica_With_Null_Manager_Initializes_And_Stores()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q =>
            {
                q.UseMemoryQueues();
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
            })
            .Build();

        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 28665 }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);

        try
        {
            HorseQueue source = rider.Queue.Find("SourceTemplate3") ?? await rider.Queue.Create("SourceTemplate3");
            Assert.NotNull(source);
            NodeQueueInfo info = source.ClusterNotifier.CreateNodeQueueInfo();
            info.Name = "PushRunningBugQueue";
            info.HandlerName = "Default";
            info.Initialized = false;

            // 1. Replica created pre-init → Manager null, Status NotInitialized.
            HorseQueue replica = await rider.Queue.CreateReplica(info);
            Assert.NotNull(replica);
            Assert.Equal(QueueStatus.NotInitialized, replica.Status);
            Assert.Null(replica.Manager);

            // 2. Advance to Running WITHOUT initializing (stray state message) → BUG STATE
            //    (identical to the proven 204 PushByNode scenario).
            replica.SetStatus(QueueStatus.Running);
            Assert.Equal(QueueStatus.Running, replica.Status);
            Assert.Null(replica.Manager);

            // 3. Client-publish Push on the Running+Manager=null replica.
            HorseMessage message = new HorseMessage(MessageType.QueueMessage, "PushRunningBugQueue");
            message.SetStringContent("push-payload");
            message.CalculateLengths();

            PushResult result;
            try
            {
                result = await replica.Push(new QueueMessage(message), null);
            }
            catch (Exception e)
            {
                _output.WriteLine($"THREW {e.GetType().Name}: {e.Message}");
                throw;
            }

            _output.WriteLine($"result={result} Status={replica.Status} Manager={(replica.Manager == null ? "NULL" : "set")} Count={replica.Manager?.MessageStore.Count()}");

            // Post-fix expectations.
            Assert.Equal(PushResult.Success, result);
            Assert.NotNull(replica.Manager);
            Assert.Equal(1, replica.Manager.MessageStore.Count());
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
        }
    }

    /// <summary>
    /// Reproduces prod eventId 209 ("Initialize In Push Queue" at HorseQueue.Push:844) via the real
    /// prod path: a PARTITION-enabled parent queue that spawns per-label sub-queues at RUNTIME from
    /// the PARTITION_LABEL header (prod: OrderUpdatedEvent-premium-Partition-&lt;companyId&gt;). Each distinct
    /// label -> PartitionManager.RouteMessage -> CreatePartition -> a fresh dynamic sub-queue that must
    /// initialize on first push. We push several distinct labels (dynamic-label churn) to exercise that
    /// sub-queue init path and surface the NRE.
    /// </summary>
    [Fact]
    public async Task Push_WithDynamicPartitionLabels_Initializes_And_Stores()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureQueues(q =>
            {
                q.UseMemoryQueues();
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
                q.Options.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoAssignWorkers = true,
                    MaxPartitionsPerWorker = 1,
                    SubscribersPerPartition = 1
                };
            })
            .Build();

        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 28670 }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);

        try
        {
            HorseQueue parent = rider.Queue.Find("TierParentQueue") ?? await rider.Queue.Create("TierParentQueue");
            Assert.NotNull(parent);

            // Dynamic-label churn: distinct PARTITION_LABEL per push -> new sub-queue each time.
            int ok = 0;
            for (int i = 0; i < 5; i++)
            {
                HorseMessage m = new HorseMessage(MessageType.QueueMessage, "TierParentQueue");
                m.AddHeader(HorseHeaders.PARTITION_LABEL, $"company-{i}");
                m.SetStringContent($"payload-{i}");
                m.CalculateLengths();

                PushResult r = await parent.Push(new QueueMessage(m), null);
                _output.WriteLine($"label=company-{i} result={r}");
                if (r == PushResult.Success)
                    ok++;
            }

            Assert.Equal(5, ok);
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
        }
    }

    /// <summary>
    /// PROD-FAITHFUL reproduce of the LIVE eventId 209 NRE ("Initialize In Push Queue: BcCargoReadyToShipEvent-standard"
    /// @ HorseQueue.Push:873) that all five tests above MISS. The difference is the queue manager:
    /// every passing test uses <c>UseMemoryQueues()</c>, but production (parrot HorseService.cs) uses
    /// <c>UsePersistentQueues(...)</c> + <c>UseCustomPersistentConfigurator(null)</c> + partition. A replica
    /// queue (Manager null, Status NotInitialized) that receives a direct client-publish Push goes through
    /// the Push init block (HorseQueue.cs:848-876); with the PERSISTENT manager the initialization NREs and
    /// is re-thrown at :874 after SendError(:873). Config mirrors parrot HorseService.cs:171-205 exactly.
    ///
    /// Pre-fix (8.2.17): Push throws NullReferenceException -> eventId 209 "Initialize In Push Queue".
    /// Post-fix: the init block initializes the persistent Manager and stores the message (Success).
    /// </summary>
    [Fact]
    public async Task Push_On_Uninitialized_Replica_PersistentQueues_ProdConfig_InitializesWithoutNre()
    {
        string dataPath = Path.Combine(Path.GetTempPath(), "horse-nre-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        CapturingErrorHandler err = new();

        // EXACT prod config (parrot src/messaging.server/HorseService.cs:140-205).
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => { o.DataPath = dataPath; })
            .ConfigureQueues(q =>
            {
                q.UsePersistentQueues(pq =>
                {
                    pq.SetAutoShrink(true, TimeSpan.FromMinutes(10));
                    pq.UseInstantFlush();
                });
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
                q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                q.Options.AcknowledgeTimeout = TimeSpan.FromMinutes(5);
                q.Options.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoDestroy = PartitionAutoDestroy.NoMessages,
                    AutoAssignWorkers = true,
                    MaxPartitionCount = 0,
                    MaxPartitionsPerWorker = 1,
                    SubscribersPerPartition = 1,
                    AutoDestroyIdleSeconds = 120
                };
                q.UseCustomPersistentConfigurator(null);
            })
            .Build();
        rider.ErrorHandlers.Add(err);

        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 28690 }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);

        try
        {
            HorseQueue source = rider.Queue.Find("SourceTemplateP") ?? await rider.Queue.Create("SourceTemplateP");
            Assert.NotNull(source);
            NodeQueueInfo info = source.ClusterNotifier.CreateNodeQueueInfo();
            info.Name = "BcCargoReadyToShipEvent-standard"; // the exact prod queue that logs 209
            info.HandlerName = "Default";
            info.Initialized = false;

            // Replica created pre-init → Manager null, Status NotInitialized (Push takes its init block).
            HorseQueue replica = await rider.Queue.CreateReplica(info);
            Assert.NotNull(replica);
            Assert.Equal(QueueStatus.NotInitialized, replica.Status);
            Assert.Null(replica.Manager);

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, "BcCargoReadyToShipEvent-standard");
            message.SetStringContent("bc-ready-payload");
            message.CalculateLengths();

            NullReferenceException thrown = null;
            PushResult result = PushResult.Success;
            try
            {
                result = await replica.Push(new QueueMessage(message), null);
            }
            catch (NullReferenceException e)
            {
                thrown = e;
                _output.WriteLine("PUSH THREW NullReferenceException:");
                _output.WriteLine(e.StackTrace ?? "(no stack)");
            }

            int nre209 = err.Errors.Count(e => e is NullReferenceException);
            _output.WriteLine($"result={result} threwNre={(thrown != null)} captured209Nre={nre209} Status={replica.Status} Manager={(replica.Manager == null ? "NULL" : "set")}");

            // Post-fix expectation (regression guard for the live prod eventId 209 NRE): the Push init
            // block no longer dereferences the null frozen QueueManagerFactories — it resolves the factory
            // via the null-safe FindQueueManagerFactory fallback and initializes the persistent Manager.
            // Pre-fix this path threw NullReferenceException at HorseQueue.cs:864 ("Initialize In Push
            // Queue: BcCargoReadyToShipEvent-standard", eventId 209) and black-holed the message.
            Assert.Null(thrown);
            Assert.DoesNotContain(err.Errors, e => e is NullReferenceException);
            Assert.NotNull(replica.Manager); // factory resolved + Manager assigned, no NRE
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
            try { Directory.Delete(dataPath, true); } catch { }
        }
    }

    /// <summary>
    /// REPRODUCE of the live prod DELIVERY DROP (not just the NRE): with the persistent manager, a message
    /// pushed to an uninitialized replica queue is silently DROPPED (server logs MESSAGE_PRODUCED but the
    /// message is never stored/delivered — prod: pending=0, no Deliver, charge stays Held). The null-guard
    /// fix stops the eventId 209 crash, but the factory pre-assigns Queue.Manager, so InitializeQueue's
    /// `if (Manager != null) return` early-return SKIPS queueManager.Initialize() + Status=Running — the
    /// PersistentQueueManager store is never opened, so the push cannot store the message.
    ///
    /// Control (A) proves the SAME setup with UseMemoryQueues() STORES the message (which is exactly why
    /// the 5 existing tests are green — MemoryQueueManager's store is usable straight from its ctor and
    /// needs no Initialize()). (B) with UsePersistentQueues() DROPS it — the untested prod path.
    /// </summary>
    [Fact]
    public async Task Push_On_Uninitialized_Replica_Memory_Stores_ButPersistent_Drops()
    {
        // ── (A) CONTROL: memory manager — message IS stored (mirrors the passing existing tests) ──
        int memoryStored = await PushToReplicaAndCountStored(useMemory: true, port: 28695, dataPath: null);
        _output.WriteLine($"[A] memory   stored={memoryStored}");

        // ── (B) PROD PATH: persistent manager — message is DROPPED (store never initialized) ──
        string dataPath = Path.Combine(Path.GetTempPath(), "horse-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        int persistentStored;
        try
        {
            persistentStored = await PushToReplicaAndCountStored(useMemory: false, port: 28696, dataPath: dataPath);
        }
        finally
        {
            try { Directory.Delete(dataPath, true); } catch { }
        }
        _output.WriteLine($"[B] persist  stored={persistentStored}");

        // Control must store; prod path must ALSO store once the deeper init-completeness fix lands.
        // Today (null-guard only) (B) is 0 → this assertion reproduces the live delivery drop.
        Assert.Equal(1, memoryStored);
        Assert.Equal(1, persistentStored);
    }

    // Creates a pre-init replica queue (Manager null, Status NotInitialized), pushes one message via the
    // client-publish Push path, and returns how many messages actually landed in the queue's store.
    private async Task<int> PushToReplicaAndCountStored(bool useMemory, int port, string dataPath)
    {
        HorseRiderBuilder builder = HorseRiderBuilder.Create();
        if (!useMemory)
            builder = builder.ConfigureOptions(o => { o.DataPath = dataPath; });

        HorseRider rider = builder
            .ConfigureQueues(q =>
            {
                if (useMemory)
                    q.UseMemoryQueues();
                else
                    q.UsePersistentQueues(pq =>
                    {
                        pq.SetAutoShrink(true, TimeSpan.FromMinutes(10));
                        pq.UseInstantFlush();
                    });
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
                q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                // NON-partitioned so the message lands directly in this queue's own store (clean count).
            })
            .Build();

        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = port }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);

        try
        {
            HorseQueue source = rider.Queue.Find("SrcTpl-" + port) ?? await rider.Queue.Create("SrcTpl-" + port);
            NodeQueueInfo info = source.ClusterNotifier.CreateNodeQueueInfo();
            info.Name = "DropRepro-" + port;
            info.HandlerName = "Default";
            info.Initialized = false;

            HorseQueue replica = await rider.Queue.CreateReplica(info);
            Assert.Equal(QueueStatus.NotInitialized, replica.Status);
            Assert.Null(replica.Manager);

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, replica.Name);
            message.SetStringContent("payload");
            message.CalculateLengths();

            PushResult result = await replica.Push(new QueueMessage(message), null);
            int stored = replica.Manager == null ? -1 : replica.Manager.MessageStore.Count();
            _output.WriteLine($"  useMemory={useMemory} result={result} Status={replica.Status} stored={stored}");
            return Math.Max(stored, 0);
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
        }
    }

    /// <summary>
    /// Regression guard for the S7 InitializeQueue guard bug on the NORMAL create path (not just replica):
    /// a plain persistent Create + push must initialize the queue (Status=Running) and STORE the message.
    /// Pre-fix the factory-pre-assigned Manager tripped `if (Manager != null) return`, so Initialize()
    /// and Status=Running were skipped → the PersistentQueueManager store was never opened → the push
    /// returned StatusNotSupported and the message was dropped (Status stayed NotInitialized). This is the
    /// exact prod delivery drop; MemoryQueueManager hid it (store usable from ctor) but even memory could
    /// not DELIVER to a consumer because Status never reached Running (see Test.Queues PushDeliveryTest).
    /// </summary>
    [Fact]
    public async Task NormalPersistentCreate_Push_InitializesAndStores()
    {
        string dataPath = Path.Combine(Path.GetTempPath(), "horse-normal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => { o.DataPath = dataPath; })
            .ConfigureQueues(q =>
            {
                q.UsePersistentQueues(pq => { pq.SetAutoShrink(true, TimeSpan.FromMinutes(10)); pq.UseInstantFlush(); });
                q.Options.Type = QueueType.RoundRobin;
                q.Options.AutoQueueCreation = true;
                q.Options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            })
            .Build();
        HorseServer server = new HorseServer();
        server.Options.Hosts = [new HorseHostOptions { Port = 28697 }];
        server.UseRider(rider);
        _ = server.StartAsync();
        await Task.Delay(500);
        try
        {
            HorseQueue q = await rider.Queue.Create("NormalQ");
            HorseMessage m = new HorseMessage(MessageType.QueueMessage, "NormalQ");
            m.SetStringContent("payload");
            m.CalculateLengths();
            PushResult r = await q.Push(new QueueMessage(m), null);
            int stored = q.Manager?.MessageStore.Count() ?? -1;
            _output.WriteLine($"result={r} Status={q.Status} stored={stored}");

            Assert.Equal(QueueStatus.Running, q.Status);
            Assert.Equal(PushResult.Success, r);
            Assert.Equal(1, stored);
        }
        finally
        {
            await server.StopAsync();
            await Task.Delay(300);
            try { Directory.Delete(dataPath, true); } catch { }
        }
    }

    private sealed class CapturingErrorHandler : IErrorHandler
    {
        private readonly object _lock = new();
        public List<Exception> Errors { get; } = new();

        public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
        {
            lock (_lock)
                Errors.Add(exception);
        }
    }

    /// <summary>
    /// Prod-faithful reproduce of eventId 209 ("Initialize In Push Queue" @ HorseQueue.Push:844):
    /// a 2-node Reliable cluster + PARTITION-enabled queue where sub-queues are created at RUNTIME
    /// from dynamic PARTITION_LABEL values (prod: *-Partition-&lt;companyId&gt;). A client on the Main
    /// publishes with distinct labels (dynamic-label churn). Each new label spawns a partition
    /// sub-queue that must initialize AND replicate to the successor. This is the exact prod churn;
    /// the single-node variants above do NOT reproduce it (no successor replication of the dynamic
    /// sub-queue). We capture Horse's SendError on every node to catch the NRE.
    /// </summary>
    [Fact]
    public async Task Cluster_Push_WithDynamicPartitionLabels_ReplicatesWithoutNre()
    {
        int[] ports = { 28681, 28682 };
        List<(HorseServer server, HorseRider rider, CapturingErrorHandler err)> nodes = new();

        for (int i = 0; i < ports.Length; i++)
        {
            int port = ports[i];
            CapturingErrorHandler err = new();

            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureQueues(q =>
                {
                    q.UseMemoryQueues();
                    q.Options.Type = QueueType.RoundRobin;
                    q.Options.AutoQueueCreation = true;
                    q.Options.Partition = new PartitionOptions
                    {
                        Enabled = true,
                        AutoAssignWorkers = true,
                        MaxPartitionsPerWorker = 1,
                        SubscribersPerPartition = 1
                    };
                })
                .Build();

            rider.ErrorHandlers.Add(err);
            rider.Cluster.Options.Name = $"Node-{port}";
            rider.Cluster.Options.SharedSecret = "top-secret";
            rider.Cluster.Options.NodeHost = $"horse://localhost:{port}";
            rider.Cluster.Options.PublicHost = $"horse://localhost:{port}";
            rider.Cluster.Options.Mode = ClusterMode.Reliable;
            rider.Cluster.Options.Acknowledge = ReplicaAcknowledge.OnlySuccessor;

            int other = ports[1 - i];
            rider.Cluster.Options.Nodes.Add(new NodeInfo
            {
                Name = $"Node-{other}",
                Host = $"horse://localhost:{other}",
                PublicHost = $"horse://localhost:{other}"
            });

            HorseServer server = new HorseServer();
            server.Options.Hosts = [new HorseHostOptions { Port = port }];
            server.UseRider(rider);
            _ = server.StartAsync();
            nodes.Add((server, rider, err));
        }

        HorseClient client = null;

        try
        {
            // Wait for election: one Main + a connected successor.
            DateTime deadline = DateTime.UtcNow.AddSeconds(25);
            while (DateTime.UtcNow < deadline &&
                   nodes.Count(n => n.rider.Cluster.State == NodeState.Main) != 1)
                await Task.Delay(250);

            (HorseServer _, HorseRider mainRider, CapturingErrorHandler _) =
                nodes.First(n => n.rider.Cluster.State == NodeState.Main);
            int mainPort = int.Parse(mainRider.Cluster.Options.NodeHost.Split(':').Last());
            _output.WriteLine($"Main={mainRider.Cluster.Options.Name} port={mainPort}");

            // give the successor time to connect as a cluster peer
            await Task.Delay(2000);

            client = new HorseClient();
            client.SetClientName("dyn-label-producer");
            await client.ConnectAsync($"horse://localhost:{mainPort}");
            Assert.True(client.IsConnected, "producer could not connect to Main");

            // Dynamic-label churn against the Main: each label -> new partition sub-queue -> replicate.
            for (int i = 0; i < 8; i++)
            {
                HorseResult r = await client.Queue.Push(
                    "DynLabelQueue",
                    new MemoryStream(Encoding.UTF8.GetBytes($"payload-{i}")),
                    false,
                    null,
                    partitionLabel: $"company-{i}",
                    cancellationToken: default);
                _output.WriteLine($"label=company-{i} pushCode={r.Code}");
                await Task.Delay(300);
            }

            await Task.Delay(2000);

            List<Exception> allErrors = nodes.SelectMany(n => n.err.Errors).ToList();
            foreach (Exception e in allErrors)
                _output.WriteLine($"ERR: {e.GetType().Name}: {e.Message}");

            Assert.DoesNotContain(allErrors, e => e is NullReferenceException);
        }
        finally
        {
            client?.Disconnect();
            foreach ((HorseServer server, HorseRider _, CapturingErrorHandler _) in nodes)
            {
                try { await server.StopAsync(); } catch { }
            }
            await Task.Delay(500);
        }
    }
}
