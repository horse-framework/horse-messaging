using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
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
}
