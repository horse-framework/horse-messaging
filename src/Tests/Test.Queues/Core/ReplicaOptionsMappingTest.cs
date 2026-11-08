using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Messaging.Server.Queues.States;
using Xunit;

namespace Test.Queues.Core;

/// <summary>
/// A replica queue must behave like the Main's queue after a failover. Creating and updating a
/// replica mapped different subsets of the options, and some options were not sent at all.
/// </summary>
public class ReplicaOptionsMappingTest
{
    /// <summary>
    /// Not a behaviour of a queue: it decides whether the server creates queues on demand
    /// </summary>
    private static readonly string[] NotReplicated = [nameof(QueueOptions.AutoQueueCreation)];

    private static HorseRider CreateRider()
    {
        string dataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rom-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dataPath);

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q => q.UseMemoryQueues())
            .Build();

        rider.Queue.OptionsConfigurator = null;
        return rider;
    }

    /// <summary>
    /// Every option differs from its default, so an option the mapping misses keeps the default and fails the comparison
    /// </summary>
    private static void SetNonDefaults(QueueOptions o)
    {
        o.Type = QueueType.Pull;
        o.Acknowledge = QueueAckDecision.JustRequest;
        o.CommitWhen = CommitWhen.AfterAcknowledge;
        o.AcknowledgeTimeout = TimeSpan.FromSeconds(7);
        o.MessageTimeout = new MessageTimeoutStrategy { MessageDuration = 33, Policy = MessageTimeoutPolicy.Delete, TargetName = "target" };
        o.MessageLimit = 11;
        o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
        o.MessageSizeLimit = 12345;
        o.ClientLimit = 9;
        o.DelayBetweenMessages = 3;
        o.PutBack = PutBackDecision.No;
        o.PutBackDelay = 77;
        o.AutoDestroy = QueueDestroy.NoMessages;
        o.MessageIdUniqueCheck = true;
        o.Partition = new PartitionOptions
        {
            Enabled = false,
            MaxPartitionCount = 4,
            SubscribersPerPartition = 2,
            AutoDestroy = PartitionAutoDestroy.Empty,
            AutoDestroyIdleSeconds = 12,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 3
        };
    }

    /// <summary>
    /// The info the replica receives: serialized and deserialized as on the wire
    /// </summary>
    private static NodeQueueInfo OverTheWire(HorseQueue source)
    {
        NodeQueueInfo info = source.ClusterNotifier.CreateNodeQueueInfo();
        string json = JsonSerializer.Serialize(info, SerializerFactory.Default());
        return JsonSerializer.Deserialize<NodeQueueInfo>(json, SerializerFactory.Default());
    }

    private static void AssertSameOptions(QueueOptions expected, QueueOptions actual)
    {
        foreach (PropertyInfo property in typeof(QueueOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (NotReplicated.Contains(property.Name))
                continue;

            object e = property.GetValue(expected);
            object a = property.GetValue(actual);

            if (e is MessageTimeoutStrategy et && a is MessageTimeoutStrategy at)
            {
                Assert.Equal((et.MessageDuration, et.Policy, et.TargetName), (at.MessageDuration, at.Policy, at.TargetName));
                continue;
            }

            if (e is PartitionOptions ep && a is PartitionOptions ap)
            {
                Assert.Equal(JsonSerializer.Serialize(ep), JsonSerializer.Serialize(ap));
                continue;
            }

            Assert.True(Equals(e, a), $"{property.Name}: expected {e}, replica has {a}");
        }
    }

    [Fact]
    public async Task CreateReplica_CopiesEveryOption()
    {
        HorseRider main = CreateRider();
        HorseQueue source = await main.Queue.Create("replicated-create", SetNonDefaults);

        HorseRider replicaNode = CreateRider();
        HorseQueue replica = await replicaNode.Queue.CreateReplica(OverTheWire(source));

        AssertSameOptions(source.Options, replica.Options);
        Assert.Equal(QueueType.Pull, replica.Type);
        Assert.IsType<PullQueueState>(replica.State);
    }

    [Fact]
    public async Task UpdateReplica_CopiesEveryOption()
    {
        HorseRider main = CreateRider();
        HorseQueue source = await main.Queue.Create("replicated-update", SetNonDefaults);

        // built from server defaults, as PushByNode does when a push arrives before the create message
        HorseRider replicaNode = CreateRider();
        HorseQueue replica = await replicaNode.Queue.Create("replicated-update");

        replica.UpdateOptionsByNodeInfo(OverTheWire(source));

        AssertSameOptions(source.Options, replica.Options);
    }

    [Fact]
    public async Task UpdateReplica_RebuildsTheStateOfADefaultBuiltCopy()
    {
        HorseRider main = CreateRider();
        HorseQueue source = await main.Queue.Create("replicated-type", SetNonDefaults);

        HorseRider replicaNode = CreateRider();
        SetNodeState(replicaNode, NodeState.Replica);
        HorseQueue replica = await replicaNode.Queue.Create("replicated-type");
        Assert.IsType<RoundRobinQueueState>(replica.State);

        replica.UpdateOptionsByNodeInfo(OverTheWire(source));

        Assert.Equal(QueueType.Pull, replica.Type);
        Assert.IsType<PullQueueState>(replica.State);
    }

    [Fact]
    public async Task UpdateOnServingNode_DoesNotSwapTheStateOfALiveQueue()
    {
        HorseRider main = CreateRider();
        HorseQueue source = await main.Queue.Create("serving-type", SetNonDefaults);

        HorseRider servingNode = CreateRider();
        HorseQueue queue = await servingNode.Queue.Create("serving-type");

        queue.UpdateOptionsByNodeInfo(OverTheWire(source));

        Assert.Equal(QueueType.RoundRobin, queue.Type);
        Assert.IsType<RoundRobinQueueState>(queue.State);
    }

    [Fact]
    public void InfoFromOlderNode_KeepsOptionsItDoesNotCarry()
    {
        QueueOptions options = new QueueOptions { CommitWhen = CommitWhen.AfterAcknowledge, PutBack = PutBackDecision.No };
        NodeQueueInfo info = new NodeQueueInfo { MessageTimeout = new MessageTimeoutStrategyInfo(0, "no-timeout") };

        info.ApplyTo(options);

        Assert.Equal(CommitWhen.AfterAcknowledge, options.CommitWhen);
        Assert.Equal(PutBackDecision.No, options.PutBack);
    }

    private static void SetNodeState(HorseRider rider, NodeState state)
    {
        typeof(ClusterManager).GetProperty(nameof(ClusterManager.State))!.SetValue(rider.Cluster, state);
    }
}
