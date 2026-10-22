using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;
using Xunit;
using Xunit.Abstractions;

namespace Test.Nodes;

/// <summary>
/// Partitioned queues in a Reliable cluster: partition sub-queues created and auto-destroyed on the
/// Main must be mirrored on the replicas as partition sub-queues, never as zombie queues that keep
/// living after the Main removed them or that own a PartitionManager of their own.
/// </summary>
public class PartitionClusterTest
{
    private readonly ITestOutputHelper _output;
    private readonly string _run = Guid.NewGuid().ToString("N").Substring(0, 8);

    public PartitionClusterTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class ClusterNode
    {
        public int Port { get; init; }
        public HorseRider Rider { get; init; }
        public HorseServer Server { get; init; }
    }

    private sealed class CollectingErrorHandler : IErrorHandler
    {
        private readonly List<string> _errors;

        public CollectingErrorHandler(List<string> errors)
        {
            _errors = errors;
        }

        public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
        {
            lock (_errors)
                _errors.Add($"{eventId} {message} {exception}");
        }
    }

    private List<ClusterNode> StartCluster(int[] ports)
    {
        List<ClusterNode> nodes = new();

        for (int i = 0; i < ports.Length; i++)
        {
            int port = ports[i];
            List<string> errors = new();

            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureQueues(q =>
                {
                    q.UseMemoryQueues();
                    q.UseCustomPersistentConfigurator(null);
                    q.Options.Type = QueueType.RoundRobin;
                    q.Options.Acknowledge = QueueAckDecision.None;
                    q.Options.AutoQueueCreation = true;
                    // Server-wide default with partitioning on, as in production: a replica that
                    // builds its copies from server defaults gives every copy a PartitionManager.
                    q.Options.Partition = new PartitionOptions
                    {
                        Enabled = true,
                        AutoDestroy = PartitionAutoDestroy.NoMessages,
                        AutoDestroyIdleSeconds = 2,
                        SubscribersPerPartition = 1
                    };
                })
                .AddErrorHandler(new CollectingErrorHandler(errors))
                .Build();

            rider.Cluster.Options.Name = $"Node-{port}";
            rider.Cluster.Options.SharedSecret = "top-secret";
            rider.Cluster.Options.NodeHost = $"horse://localhost:{port}";
            rider.Cluster.Options.PublicHost = $"horse://localhost:{port}";
            rider.Cluster.Options.Mode = ClusterMode.Reliable;
            rider.Cluster.Options.Acknowledge = ReplicaAcknowledge.None;

            foreach (int other in ports.Where(p => p != port))
            {
                rider.Cluster.Options.Nodes.Add(new NodeInfo
                {
                    Name = $"Node-{other}",
                    Host = $"horse://localhost:{other}",
                    PublicHost = $"horse://localhost:{other}"
                });
            }

            HorseServer server = new HorseServer();
            server.Options.Hosts = [new HorseHostOptions { Port = port }];
            server.UseRider(rider);
            _ = server.StartAsync();

            ClusterNode node = new ClusterNode { Port = port, Rider = rider, Server = server };
            nodes.Add(node);
            _errorLists[node] = errors;
        }

        return nodes;
    }

    private readonly Dictionary<ClusterNode, List<string>> _errorLists = new();

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

    private static async Task<(ClusterNode main, ClusterNode successor)> WaitForConnectedSuccessor(List<ClusterNode> nodes)
    {
        bool ready = await WaitUntil(() =>
        {
            ClusterNode m = nodes.FirstOrDefault(n => n.Rider.Cluster.State == NodeState.Main);
            ClusterNode s = nodes.FirstOrDefault(n => n.Rider.Cluster.State == NodeState.Successor);
            if (m == null || s == null || m.Rider.Cluster.SuccessorNode == null)
                return false;

            return m.Rider.Cluster.Clients.Any(c => c.IsConnected && c.Info.Id == m.Rider.Cluster.SuccessorNode.Id);
        });

        Assert.True(ready, "cluster did not reach Main + connected-Successor");
        return (nodes.First(n => n.Rider.Cluster.State == NodeState.Main),
            nodes.First(n => n.Rider.Cluster.State == NodeState.Successor));
    }

    /// <summary>
    /// Simulates a crashed node. Stopping the server alone is not enough in-process: the stopped
    /// rider's outgoing node clients stay connected to the peers, so they never see the node go down.
    /// </summary>
    private static async Task KillNode(ClusterNode node)
    {
        await node.Server.StopAsync();

        foreach (NodeClient client in node.Rider.Cluster.Clients)
        {
            (typeof(NodeClient).GetField("_outgoingClient", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(client) as HorseClient)?.Disconnect();
            (typeof(NodeClient).GetField("_incomingClient", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(client) as MessagingClient)?.Disconnect();
        }
    }

    private static Task<PushResult> PushLabeled(HorseQueue parent, string label)
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, parent.Name);
        msg.SetStringContent("payload");
        msg.AddHeader(HorseHeaders.PARTITION_LABEL, label);
        return parent.Push(msg);
    }

    private void DumpErrors()
    {
        foreach (var (node, errors) in _errorLists)
        {
            lock (errors)
                foreach (string e in errors)
                    _output.WriteLine($"[{node.Port}] {e}");
        }
    }

    private async Task<(ClusterNode main, ClusterNode successor, HorseQueue parent)> StartWithParent(List<ClusterNode> nodes, string parentName)
    {
        (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);

        // Created programmatically, like CreatePartition creates sub-queues. Such queues used to
        // carry a null ManagerName, and the replica's CreateReplica threw on it silently.
        HorseQueue parent = await main.Rider.Queue.Create(parentName);
        Assert.True(parent.IsPartitioned);
        Assert.True(await WaitUntil(() => successor.Rider.Queue.Find(parentName) != null, 10000), "parent not replicated");
        Assert.True(successor.Rider.Queue.Find(parentName).IsPartitioned, "replica parent is not partitioned");
        return (main, successor, parent);
    }

    private static async Task<HorseQueue> PushAndWaitForReplica(HorseQueue parent, ClusterNode successor, string subName)
    {
        Assert.Equal(PushResult.Success, await PushLabeled(parent, "tenant1"));
        Assert.True(await WaitUntil(() => successor.Rider.Queue.Find(subName) != null, 10000), "sub-queue not replicated");

        HorseQueue replicaSub = successor.Rider.Queue.Find(subName);
        Assert.True(replicaSub.IsPartitionQueue, "replica copy is not marked as a partition sub-queue");
        Assert.False(replicaSub.IsPartitioned, "replica copy owns a PartitionManager of its own");
        Assert.Equal(parent.Name, replicaSub.PartitionMeta?.ParentQueueName);
        return replicaSub;
    }

    [Fact]
    public async Task PartitionDestroyedOnMain_IsRemovedOnReplica_AndReplicaIsAPartitionQueue()
    {
        List<ClusterNode> nodes = StartCluster(new[] { 28811, 28812 });

        try
        {
            string parentName = $"InvoiceEvent-{_run}";
            string subName = $"{parentName}-Partition-tenant1";
            (ClusterNode main, ClusterNode successor, HorseQueue parent) = await StartWithParent(nodes, parentName);

            for (int cycle = 0; cycle < 2; cycle++)
            {
                await PushAndWaitForReplica(parent, successor, subName);

                // Drain on Main so the partition becomes idle, then auto-destroy removes it.
                main.Rider.Queue.Find(subName).Manager.MessageStore.Clear();
                Assert.True(await WaitUntil(() => main.Rider.Queue.Find(subName) == null, 15000), "partition not auto-destroyed on main");
                Assert.True(await WaitUntil(() => successor.Rider.Queue.Find(subName) == null, 10000), "zombie: replica kept the destroyed partition");
            }
        }
        finally
        {
            DumpErrors();
            await StopCluster(nodes);
        }
    }

    [Fact]
    public async Task DestroyStepThrowsOnMain_ReplicaStillRemovesPartition()
    {
        List<ClusterNode> nodes = StartCluster(new[] { 28821, 28822 });

        try
        {
            string parentName = $"WalletEvent-{_run}";
            string subName = $"{parentName}-Partition-tenant1";
            (ClusterNode main, ClusterNode successor, HorseQueue parent) = await StartWithParent(nodes, parentName);
            await PushAndWaitForReplica(parent, successor, subName);

            HorseQueue mainSub = main.Rider.Queue.Find(subName);
            mainSub.OnDestroyed += _ => throw new InvalidOperationException("destroy step failed");
            mainSub.Manager.MessageStore.Clear();

            Assert.True(await WaitUntil(() => main.Rider.Queue.Find(subName) == null, 15000), "partition not auto-destroyed on main");
            Assert.True(await WaitUntil(() => successor.Rider.Queue.Find(subName) == null, 10000), "zombie: a failing destroy step kept the replica copy alive");
        }
        finally
        {
            DumpErrors();
            await StopCluster(nodes);
        }
    }

    [Fact]
    public async Task MainFails_PromotedNodeAdoptsMirroredPartition_AndStoresNewMessages()
    {
        List<ClusterNode> nodes = StartCluster(new[] { 28831, 28832 });

        try
        {
            string parentName = $"CargoEvent-{_run}";
            string subName = $"{parentName}-Partition-tenant1";
            (ClusterNode main, ClusterNode successor, HorseQueue parent) = await StartWithParent(nodes, parentName);
            HorseQueue replicaSub = await PushAndWaitForReplica(parent, successor, subName);
            Assert.True(await WaitUntil(() => replicaSub.Manager?.MessageStore.Count() == 1, 10000), "message not replicated");

            await KillNode(main);
            Assert.True(await WaitUntil(() => successor.Rider.Cluster.State is NodeState.Single or NodeState.Main, 30000), "survivor never took over");

            HorseQueue survivorParent = successor.Rider.Queue.Find(parentName);
            Assert.True(await WaitUntil(() => survivorParent.PartitionManager.Partitions.Any(p => p.Queue == replicaSub), 10000),
                "promoted node did not attach the mirrored partition to its parent");

            Assert.Equal(PushResult.Success, await PushLabeled(survivorParent, "tenant1"));
            Assert.Same(replicaSub, successor.Rider.Queue.Find(subName));
            Assert.Equal(2, replicaSub.Manager.MessageStore.Count());
        }
        finally
        {
            DumpErrors();
            await StopCluster(nodes);
        }
    }

    /// <summary>
    /// A replica that is not waited on (here ReplicaAcknowledge.None, in production the replicas
    /// beside the successor) receives the replicated push and, after a fast consumer ack, the
    /// message removal on the same link. Both used to run concurrently: when the removal ran before
    /// the push had stored the message, it removed nothing and the copy stayed on the replica.
    /// </summary>
    [Fact]
    public async Task RemovalArrivingWhileReplicaStillStoresPush_IsAppliedAfterThePush()
    {
        List<ClusterNode> nodes = StartCluster(new[] { 28841, 28842 });

        try
        {
            (ClusterNode main, ClusterNode successor) = await WaitForConnectedSuccessor(nodes);

            string name = $"OrderEvent-{_run}";
            HorseQueue mainQueue = await main.Rider.Queue.Create(name, o => o.Partition = null);
            Assert.True(await WaitUntil(() => successor.Rider.Queue.Find(name)?.Manager != null, 10000), "queue not replicated");

            HorseQueue replica = successor.Rider.Queue.Find(name);
            BlockingProducerHandler handler = new(replica.Manager.DeliveryHandler);
            replica.Manager.GetType().GetProperty(nameof(IHorseQueueManager.DeliveryHandler)).SetValue(replica.Manager, handler);

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, name);
            message.SetMessageId($"msg-{_run}");
            message.SetStringContent("consumed-on-main");
            Assert.Equal(PushResult.Success, await mainQueue.Push(message));

            try
            {
                await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

                // The consumer acks on the Main while the replica is still storing the push.
                QueueMessage stored = mainQueue.Manager.MessageStore.Find(message.MessageId);
                Assert.NotNull(stored);
                await mainQueue.RemoveMessage(stored);
                await Task.Delay(500);
            }
            finally
            {
                handler.Release.TrySetResult(true);
            }

            // Checked once the push has finished instead of polling: a later sync pass may clean
            // the copy up in this harness, but in production it stayed until the queue was removed.
            Assert.True(await WaitUntil(() => replica.Info.ReceivedMessages > 0, 5000), "replica never finished the push");
            await Task.Delay(300);
            Assert.Equal(0, replica.Manager.MessageStore.Count());
        }
        finally
        {
            DumpErrors();
            await StopCluster(nodes);
        }
    }

    private sealed class BlockingProducerHandler(IQueueDeliveryHandler inner) : IQueueDeliveryHandler
    {
        public IHorseQueueManager Manager => inner.Manager;
        public IDeliveryTracker Tracker => inner.Tracker;
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            Entered.TrySetResult(true);
            await Release.Task;
            return await inner.ReceivedFromProducer(queue, message, sender);
        }

        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message) => inner.BeginSend(queue, message);
        public Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
            => inner.CanConsumerReceive(queue, message, receiver);
        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
            => inner.ConsumerReceiveFailed(queue, delivery, receiver);
        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message) => inner.EndSend(queue, message);
        public Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery) => inner.AcknowledgeTimeout(queue, delivery);
        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
            => inner.AcknowledgeReceived(queue, acknowledgeMessage, delivery, success);
    }
}
