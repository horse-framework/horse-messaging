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
                // Mirror prod: partition-enabled queues (TrackDeliveryEvent-Partition-*).
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
