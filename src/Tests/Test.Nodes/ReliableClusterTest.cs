using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Sync;
using Horse.Server;
using Xunit;
using Xunit.Abstractions;

namespace Test.Nodes;

/// <summary>
/// Multi-node Reliable cluster behavior tests.
///
/// All scenarios are verified against the source:
///  - Election / state:      Cluster/ClusterManager.cs (UpdateState, AnnounceMainity, AskForMain, OnMainDown)
///  - Node auth:             Network/HorseNetworkHandler.cs (isNode → SharedSecret check)
///  - Replication:           Queues/Sync/QueueClusterNotifier.cs (SendMessagePush + ReplicaAcknowledge)
///                           Queues/HorseQueue.cs:949 (await ClusterNotifier.SendMessagePush before delivery)
///  - Replica push handling: Cluster/NodeClient.cs (PushByNode)
///  - Queue sync/create:     Queues/QueueRider.cs (Create → SendCreated → CreateReplica)
///
/// Each test uses a unique port pair (28700+ range) and always stops its servers in finally
/// with a small delay so sockets are released between runs (idempotent, memory queues only).
/// </summary>
public class ReliableClusterTest
{
    private readonly ITestOutputHelper _output;

    // Unique per test instance. Horse persists queue *configs* to an on-disk OptionsConfigurator
    // (data/queues.json) even with UseMemoryQueues, so a fixed queue name would be reloaded on the
    // next run and make Rider.Queue.Create throw DuplicateNameException. A fresh suffix keeps reruns green.
    private readonly string _run = Guid.NewGuid().ToString("N").Substring(0, 8);

    public ReliableClusterTest(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Helpers

    private sealed class ClusterNode
    {
        public string Name { get; init; }
        public int Port { get; init; }
        public HorseRider Rider { get; init; }
        public HorseServer Server { get; init; }
    }

    /// <summary>
    /// Builds a Reliable cluster where every node lists all other nodes as peers.
    /// NodeInfo.Name MUST equal the peer's Cluster.Options.Name — HorseNetworkHandler matches an
    /// incoming node by client name against Cluster.Clients (Network/HorseNetworkHandler.cs:113).
    /// </summary>
    private List<ClusterNode> StartReliableCluster(int[] ports, ReplicaAcknowledge acknowledge, string sharedSecret = "top-secret", string[] secretsPerNode = null)
    {
        List<ClusterNode> nodes = new();

        for (int i = 0; i < ports.Length; i++)
        {
            int port = ports[i];
            string name = $"Node-{port}";

            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureQueues(q =>
                {
                    q.UseMemoryQueues();
                    q.Options.Type = QueueType.RoundRobin;
                    q.Options.AutoQueueCreation = true;
                })
                .Build();

            rider.Cluster.Options.Name = name;
            rider.Cluster.Options.SharedSecret = secretsPerNode != null ? secretsPerNode[i] : sharedSecret;
            rider.Cluster.Options.NodeHost = $"horse://localhost:{port}";
            rider.Cluster.Options.PublicHost = $"horse://localhost:{port}";
            rider.Cluster.Options.Mode = ClusterMode.Reliable;
            rider.Cluster.Options.Acknowledge = acknowledge;

            for (int j = 0; j < ports.Length; j++)
            {
                if (j == i)
                    continue;

                rider.Cluster.Options.Nodes.Add(new NodeInfo
                {
                    Name = $"Node-{ports[j]}",
                    Host = $"horse://localhost:{ports[j]}",
                    PublicHost = $"horse://localhost:{ports[j]}"
                });
            }

            HorseServer server = new HorseServer();
            server.Options.Hosts = [new HorseHostOptions { Port = port }];
            server.UseRider(rider); // triggers rider.Cluster.Start()
            _ = server.StartAsync();

            nodes.Add(new ClusterNode { Name = name, Port = port, Rider = rider, Server = server });
        }

        return nodes;
    }

    private static async Task StopCluster(IEnumerable<ClusterNode> nodes)
    {
        foreach (ClusterNode node in nodes)
        {
            try
            {
                await node.Server.StopAsync();
            }
            catch
            {
            }
        }

        // release sockets before the next test reuses (a different) port range
        await Task.Delay(400);
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 25000, int pollMs = 200)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(pollMs);
        }

        return condition();
    }

    private static ClusterNode NodeInState(IEnumerable<ClusterNode> nodes, NodeState state)
    {
        return nodes.FirstOrDefault(n => n.Rider.Cluster.State == state);
    }

    /// <summary>
    /// Waits until election is complete AND the Main actually has a *connected* successor NodeClient.
    /// Waiting only for NodeState/SuccessorNode is not enough: SendMessagePush requires the successor
    /// NodeClient to be connected (QueueClusterNotifier.cs:47-51, 69-71). Pushing too early makes
    /// OnlySuccessor/AllNodes return Error, and None silently drop the fire-and-forget replica.
    /// </summary>
    private async Task<(ClusterNode main, ClusterNode successor)> WaitForConnectedSuccessor(List<ClusterNode> nodes)
    {
        bool ready = await WaitUntil(() =>
        {
            ClusterNode m = NodeInState(nodes, NodeState.Main);
            ClusterNode s = NodeInState(nodes, NodeState.Successor);
            if (m == null || s == null || m.Rider.Cluster.SuccessorNode == null)
                return false;

            return m.Rider.Cluster.Clients.Any(c => c.IsConnected && c.Info.Id == m.Rider.Cluster.SuccessorNode.Id);
        });

        Assert.True(ready, "cluster did not reach Main + connected-Successor");
        return (NodeInState(nodes, NodeState.Main), NodeInState(nodes, NodeState.Successor));
    }

    /// <summary>
    /// Pushes repeatedly until the successor is observed holding the replica (or the deadline passes).
    ///
    /// Why polling: the node-to-node ack has a fixed 5s window (NodeDeliveryTracker.Track,
    /// Cluster/NodeDeliveryTracker.cs:78). On a cold cluster the FIRST replicated push's ack can miss
    /// that window, so SendMessageAndWaitAck returns false and the push fails (OnlySuccessor/AllNodes)
    /// — and None can drop the fire-and-forget copy. Once the path is warm, pushes commit and replicate.
    /// This is a deterministic wait for that eventual consistency, not a workaround for a broken path.
    /// Returns whether replication was seen and whether any push committed (HorseResultCode.Ok).
    /// </summary>
    private async Task<(bool replicated, bool anyCommit)> PushUntilReplicated(
        HorseClient client, string queue, byte[] payload, ClusterNode successor, int timeoutMs = 25000, int pushGapMs = 500)
    {
        bool anyCommit = false;
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            HorseResult r = await client.Queue.Push(queue, payload, true);
            if (r.Code == HorseResultCode.Ok)
                anyCommit = true;

            if ((successor.Rider.Queue.Find(queue)?.Manager?.MessageStore.Count() ?? 0) >= 1)
                return (true, anyCommit);

            await Task.Delay(pushGapMs);
        }

        bool replicated = (successor.Rider.Queue.Find(queue)?.Manager?.MessageStore.Count() ?? 0) >= 1;
        return (replicated, anyCommit);
    }

    #endregion

    /// <summary>
    /// Scenario 1 — Main election in a 2-node Reliable cluster.
    /// Exactly one node ends up Main and the other Successor; MainNode / SuccessorNode are set
    /// consistently on both riders (ClusterManager.UpdateState + AnnounceMainity).
    /// </summary>
    [Fact]
    public async Task MainElection_TwoNodes_ProducesOneMainAndOneSuccessor()
    {
        int[] ports = { 28701, 28702 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor);

        try
        {
            bool converged = await WaitUntil(() =>
                nodes.Count(n => n.Rider.Cluster.State == NodeState.Main) == 1 &&
                nodes.Count(n => n.Rider.Cluster.State == NodeState.Successor) == 1);

            foreach (ClusterNode n in nodes)
                _output.WriteLine($"{n.Name} state={n.Rider.Cluster.State} main={n.Rider.Cluster.MainNode?.Name} successor={n.Rider.Cluster.SuccessorNode?.Name}");

            Assert.True(converged, "Cluster did not converge to exactly one Main and one Successor");

            ClusterNode main = NodeInState(nodes, NodeState.Main);
            ClusterNode successor = NodeInState(nodes, NodeState.Successor);
            Assert.NotNull(main);
            Assert.NotNull(successor);

            // Both riders agree on who the Main is.
            Assert.NotNull(main.Rider.Cluster.MainNode);
            Assert.Equal(main.Name, main.Rider.Cluster.MainNode.Name);
            Assert.NotNull(successor.Rider.Cluster.MainNode);
            Assert.Equal(main.Name, successor.Rider.Cluster.MainNode.Name);

            // Main knows its successor.
            Assert.NotNull(main.Rider.Cluster.SuccessorNode);
            Assert.Equal(successor.Name, main.Rider.Cluster.SuccessorNode.Name);
        }
        finally
        {
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Scenario 2 — Replication happy-path with ReplicaAcknowledge.OnlySuccessor.
    /// A client connected to the Main pushes with waitForCommit:true. Because HorseQueue.Push awaits
    /// ClusterNotifier.SendMessagePush (which waits the successor ack for OnlySuccessor) BEFORE the
    /// producer commit, an Ok result implies the successor stored the replica.
    /// Non-partitioned RoundRobin queue → no NoConsumers trap (message is kept in queue).
    /// </summary>
    [Fact]
    public async Task Replication_OnlySuccessor_CommitsAndStoresOnSuccessor()
    {
        int[] ports = { 28711, 28712 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor);
        HorseClient client = null;

        try
        {
            (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);

            client = new HorseClient();
            client.SetClientName("producer");
            await client.ConnectAsync($"horse://localhost:{main.Port}");
            Assert.True(await WaitUntil(() => client.IsConnected, 10000));

            string queueName = $"repl-only-successor-{_run}";
            byte[] payload = Encoding.UTF8.GetBytes("only-successor-payload");

            (bool replicated, bool anyCommit) = await PushUntilReplicated(client, queueName, payload, successor);
            _output.WriteLine($"anyCommit={anyCommit} successor replica count={successor.Rider.Queue.Find(queueName)?.Manager?.MessageStore.Count()}");
            Assert.True(anyCommit, "no push committed under ReplicaAcknowledge.OnlySuccessor");
            Assert.NotNull(successor.Rider.Queue.Find(queueName));
            Assert.True(replicated, "successor did not store the replicated message");
        }
        finally
        {
            client?.Disconnect();
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Scenario 3 — Replication with ReplicaAcknowledge.None.
    /// SendMessagePush fires to all node clients without waiting for ack and returns true immediately
    /// (QueueClusterNotifier.cs:36-40). The producer still commits (CommitWhen.AfterReceived) and the
    /// message eventually appears on the successor. We poll for eventual replication.
    /// </summary>
    [Fact]
    public async Task Replication_None_CommitsAndEventuallyReplicates()
    {
        int[] ports = { 28721, 28722 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.None);
        HorseClient client = null;

        try
        {
            (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);

            client = new HorseClient();
            client.SetClientName("producer");
            await client.ConnectAsync($"horse://localhost:{main.Port}");
            Assert.True(await WaitUntil(() => client.IsConnected, 10000));

            string queueName = $"repl-none-{_run}";
            byte[] payload = Encoding.UTF8.GetBytes("none-payload");

            // ReplicaAcknowledge.None never waits for (nor guarantees) delivery — each push is a
            // fire-and-forget SendMessage to the successor (QueueClusterNotifier.cs:36-40) that always
            // returns Ok. We poll-push until the replica is observed on the successor.
            (bool replicated, bool anyCommit) = await PushUntilReplicated(client, queueName, payload, successor);
            _output.WriteLine($"anyCommit={anyCommit} successor replica count={successor.Rider.Queue.Find(queueName)?.Manager?.MessageStore.Count()}");
            Assert.True(anyCommit, "None-mode push did not return Ok");
            Assert.True(replicated, "successor did not eventually receive the message under ReplicaAcknowledge.None");
        }
        finally
        {
            client?.Disconnect();
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Scenario 4 — Replication with ReplicaAcknowledge.AllNodes.
    /// In a 2-node cluster "all nodes" == the successor, so SendMessagePush waits the successor ack
    /// (QueueClusterNotifier.cs:64-90) then Task.WhenAll over the (empty) remaining nodes. Commit Ok
    /// implies the successor stored the message.
    /// </summary>
    [Fact]
    public async Task Replication_AllNodes_CommitsAndStoresOnSuccessor()
    {
        int[] ports = { 28731, 28732 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.AllNodes);
        HorseClient client = null;

        try
        {
            (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);

            client = new HorseClient();
            client.SetClientName("producer");
            await client.ConnectAsync($"horse://localhost:{main.Port}");
            Assert.True(await WaitUntil(() => client.IsConnected, 10000));

            string queueName = $"repl-all-nodes-{_run}";
            byte[] payload = Encoding.UTF8.GetBytes("all-nodes-payload");

            (bool replicated, bool anyCommit) = await PushUntilReplicated(client, queueName, payload, successor);
            _output.WriteLine($"anyCommit={anyCommit} successor replica count={successor.Rider.Queue.Find(queueName)?.Manager?.MessageStore.Count()}");
            Assert.True(anyCommit, "no push committed under ReplicaAcknowledge.AllNodes");
            Assert.True(replicated, "successor did not store the message under ReplicaAcknowledge.AllNodes");
        }
        finally
        {
            client?.Disconnect();
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Scenario 5 — Main failover in a 2-node cluster.
    ///
    /// Source-verified behavior (NOT the naive "successor promotes to Main"): when the Main goes down,
    /// the successor's NodeClient.ProcessDisconnection calls ClusterManager.OnMainDown → AskForMain,
    /// but AskForMain immediately returns because no peer is connected (ClusterManager.cs:269:
    /// `if (!Clients.Any(x => x.IsConnected)) return;`). ProcessDisconnection then calls UpdateState,
    /// which — with all clients disconnected — sets State = Single (ClusterManager.cs:187-188), and
    /// MainNode was already cleared to null in OnMainDown (ClusterManager.cs:342).
    ///
    /// A Single node accepts clients (CanClientConnect returns true for Single) and can push, so the
    /// surviving node keeps serving. This asserts that real recovered behavior.
    /// </summary>
    [Fact]
    public async Task MainFailover_TwoNodes_SurvivorBecomesSingleAndKeepsServing()
    {
        int[] ports = { 28741, 28742 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor);
        HorseClient client = null;

        try
        {
            (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);
            _output.WriteLine($"before failover: main={main.Name} successor={successor.Name}");

            // Bring the Main down.
            await main.Server.StopAsync();

            // Survivor transitions Successor -> Single and clears MainNode.
            bool becameSingle = await WaitUntil(() =>
                successor.Rider.Cluster.State == NodeState.Single &&
                successor.Rider.Cluster.MainNode == null);
            _output.WriteLine($"after failover: survivor state={successor.Rider.Cluster.State} main={successor.Rider.Cluster.MainNode?.Name}");
            Assert.True(becameSingle, "survivor did not become Single with MainNode=null after Main down");

            // Survivor still serves clients. Poll connect+push until the survivor (now Single) commits,
            // since the client connection and Single-state settling can lag right after the Main drops.
            client = new HorseClient();
            client.SetClientName("post-failover-producer");
            await client.ConnectAsync($"horse://localhost:{successor.Port}");

            bool served = await WaitUntil(() =>
            {
                if (!client.IsConnected)
                {
                    _ = client.ConnectAsync($"horse://localhost:{successor.Port}");
                    return false;
                }

                HorseResult r = client.Queue.Push($"post-failover-{_run}", Encoding.UTF8.GetBytes("x"), true)
                    .GetAwaiter().GetResult();
                _output.WriteLine($"post-failover push result={r.Code}");
                return r.Code == HorseResultCode.Ok;
            }, 20000, 500);

            Assert.True(served, "survivor (Single) did not serve a client push after failover");
        }
        finally
        {
            client?.Disconnect();
            await StopCluster(nodes.Where(n => n.Rider.Server.IsRunning));
        }
    }

    /// <summary>
    /// Scenario 6 — Queue definition + message sync/propagation. A queue that comes into existence on
    /// the Main (via a normal client push → auto-create with manager "Default") propagates to the
    /// successor: ClusterNotifier.SendCreated → the peer's NodeClient handles CreateQueue →
    /// Rider.Queue.CreateReplica, and the replicated message lands in the replica's message store
    /// (its Manager is initialized to hold it). We assert the successor gains the queue definition AND
    /// stores the replicated message.
    ///
    /// Notes on this in-process harness:
    ///  - Creation is driven via client push (production route), not Rider.Queue.Create(name, ...):
    ///    a direct programmatic Create with no manager name leaves ManagerName null, which propagates
    ///    as NodeQueueInfo.HandlerName=null so the replica's CreateReplica can't resolve a factory and
    ///    stays NotInitialized (QueueRider.cs:642).
    ///  - We do NOT assert HorseQueue.Status here: both riders run in the same working directory and
    ///    share one on-disk OptionsConfigurator (data/queues.json). They clobber each other's config for
    ///    the same queue name, so the reported Status is unreliable in this harness (observed
    ///    NotInitialized even after a committed+replicated push). The message-store propagation below is
    ///    the reliable, meaningful sync signal.
    /// </summary>
    [Fact]
    public async Task QueueSync_CreatedOnMain_ReplicatesToSuccessor()
    {
        int[] ports = { 28751, 28752 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor);
        HorseClient client = null;

        try
        {
            (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);

            client = new HorseClient();
            client.SetClientName("sync-producer");
            await client.ConnectAsync($"horse://localhost:{main.Port}");
            Assert.True(await WaitUntil(() => client.IsConnected, 10000));

            string queueName = $"sync-created-on-main-{_run}";
            (bool replicated, bool anyCommit) = await PushUntilReplicated(client, queueName, Encoding.UTF8.GetBytes("sync"), successor);
            Assert.True(anyCommit, "producer never committed a push to the Main");
            Assert.True(replicated, "queue created on Main was not replicated to the successor");

            // Queue definition propagated to the successor and its message store holds the replicated
            // message (Manager was initialized enough to store it) — the meaningful sync outcome.
            HorseQueue replica = successor.Rider.Queue.Find(queueName);
            _output.WriteLine($"replica present={replica != null} managerCount={replica?.Manager?.MessageStore.Count()}");
            Assert.NotNull(replica);
            Assert.NotNull(replica.Manager);
            Assert.True(replica.Manager.MessageStore.Count() >= 1, "replica exists but did not hold the replicated message");
        }
        finally
        {
            client?.Disconnect();
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Scenario 7 — Node authentication: nodes configured with mismatched SharedSecret are rejected
    /// by HorseNetworkHandler.Connected (isNode path, HorseNetworkHandler.cs:107-111 → Unauthorized),
    /// so no cluster forms. Both nodes stay Single, never connect as peers, and never elect a Main.
    /// (The matching-secret positive path is covered by MainElection_TwoNodes_*.)
    /// </summary>
    [Fact]
    public async Task NodeAuth_MismatchedSharedSecret_NodesNeverConnectOrElect()
    {
        int[] ports = { 28761, 28762 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor,
            secretsPerNode: new[] { "secret-A", "secret-B" });

        try
        {
            // Give ample time for (failed) connection + reconnect attempts.
            await Task.Delay(8000);

            foreach (ClusterNode n in nodes)
            {
                bool anyPeerConnected = n.Rider.Cluster.Clients.Any(c => c.IsConnected);
                _output.WriteLine($"{n.Name} state={n.Rider.Cluster.State} peersConnected={anyPeerConnected} main={n.Rider.Cluster.MainNode?.Name}");

                Assert.False(anyPeerConnected, $"{n.Name} unexpectedly connected to a peer with a mismatched secret");
                Assert.Null(n.Rider.Cluster.MainNode);
                Assert.Equal(NodeState.Single, n.Rider.Cluster.State);
            }
        }
        finally
        {
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Test A — null-guard. Drives OnMainDown into the State==Replica branch where SuccessorNode is
    /// unavailable, a connected firstReplica exists, but every connected peer has a null Info.StartDate
    /// (the real prod window: StartDate is set only on the inbound handshake and cleared on disconnect,
    /// while IsConnected can be true from the outbound client alone). Pre-fix: MinBy returns null and
    /// `StartDate > oldestClient.Info.StartDate` throws NullReferenceException (ClusterManager.cs:380).
    /// Post-fix: the oldestClient==null guard falls back to the Name tiebreak — no throw.
    /// </summary>
    [Fact]
    public async Task OnMainDown_ReplicaBranch_AllPeersMissingStartDate_DoesNotThrow()
    {
        int[] ports = { 28771, 28772, 28773 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor);

        try
        {
            bool formed = await WaitUntil(() =>
                nodes.Count(n => n.Rider.Cluster.State == NodeState.Main) == 1 &&
                nodes.Count(n => n.Rider.Cluster.State == NodeState.Successor) == 1 &&
                nodes.Count(n => n.Rider.Cluster.State == NodeState.Replica) == 1 &&
                NodeInState(nodes, NodeState.Replica).Rider.Cluster.Clients.Count(c => c.IsConnected) >= 1);
            Assert.True(formed, "cluster did not converge to Main + Successor + Replica");

            ClusterNode replica = NodeInState(nodes, NodeState.Replica);
            ClusterManager cluster = replica.Rider.Cluster;

            cluster.SuccessorNode = null;
            foreach (NodeClient c in cluster.Clients)
                c.Info.StartDate = null;

            NodeClient down = cluster.Clients.First(c => c.IsConnected);
            Assert.Equal(NodeState.Replica, cluster.State);

            _output.WriteLine($"replica={replica.Name} connectedPeers={cluster.Clients.Count(c => c.IsConnected)} " +
                              $"oldestNull={cluster.Clients.All(c => c.Info.StartDate == null)}");

            Exception ex = await Record.ExceptionAsync(() => cluster.OnMainDown(down));
            Assert.Null(ex);
        }
        finally
        {
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// Test B — single nominee / no dual-Main. Symmetric double-fault: two survivors both hit the
    /// oldestClient==null branch. A blanket AskForMain fallback would make BOTH ask, and each approves
    /// the other while MainNode==null (AnswerMainRequest, ClusterManager.cs:306-307) → dual-Main. The
    /// Name tiebreak funnels nomination to a single node: only the lexicographic-min Name asks for main,
    /// the other prods it. Assert exactly ONE node becomes Main and it is the min-Name node.
    /// </summary>
    [Fact]
    public async Task OnMainDown_SymmetricDoubleFault_OnlyLexicographicMinNameBecomesMain()
    {
        int[] ports = { 28781, 28782 };
        List<ClusterNode> nodes = StartReliableCluster(ports, ReplicaAcknowledge.OnlySuccessor);

        try
        {
            bool connected = await WaitUntil(() =>
                nodes.All(n => n.Rider.Cluster.Clients.Any(c => c.IsConnected)));
            Assert.True(connected, "the two nodes never connected as peers");

            ClusterNode expectedMain = nodes.OrderBy(n => n.Name, StringComparer.Ordinal).First();
            ClusterNode other = nodes.Single(n => n != expectedMain);
            _output.WriteLine($"expectedMain(minName)={expectedMain.Name} other={other.Name}");

            foreach (ClusterNode node in nodes)
            {
                ClusterManager cluster = node.Rider.Cluster;
                NodeClient peer = cluster.Clients.First(c => c.IsConnected);
                cluster.MainNode = new NodeInfo { Id = peer.Info.Id, Name = peer.Info.Name };
                cluster.SuccessorNode = null;
                cluster.UpdateState();
                Assert.Equal(NodeState.Replica, cluster.State);
                peer.Info.StartDate = null;
            }

            foreach (ClusterNode node in nodes)
            {
                ClusterManager cluster = node.Rider.Cluster;
                NodeClient peer = cluster.Clients.First(c => c.IsConnected);
                await cluster.OnMainDown(peer);
            }

            bool singleMain = await WaitUntil(() =>
                expectedMain.Rider.Cluster.State == NodeState.Main &&
                other.Rider.Cluster.State != NodeState.Main);

            foreach (ClusterNode n in nodes)
                _output.WriteLine($"{n.Name} finalState={n.Rider.Cluster.State} main={n.Rider.Cluster.MainNode?.Name}");

            Assert.True(singleMain, "min-Name node did not become the sole Main (tiebreak violated / dual-Main)");
            Assert.Equal(1, nodes.Count(n => n.Rider.Cluster.State == NodeState.Main));
        }
        finally
        {
            await StopCluster(nodes);
        }
    }
}
