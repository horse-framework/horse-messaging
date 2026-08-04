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
/// Regression tests for the "orphaned partition" lock observed in production.
///
/// Scenario: a tenant partition is served by exactly one worker
/// (SubscribersPerPartition = 1). The worker goes away (pod restart / rollout),
/// but the partition still holds messages, so AutoDestroy = NoMessages never
/// destroys it. A replacement worker subscribes label-less and must take the
/// partition over.
///
/// If the departed worker still occupies the single subscriber slot, the
/// partition is considered "full" forever: no worker is ever assigned again,
/// messages pile up until they expire, and the flow silently dies with no
/// error anywhere.
/// </summary>
public class PartitionOrphanReassignTest
{
    #region Helpers

    private const string QueueName = "orphan-q";

    private static async Task<(PartitionTestContext ctx, HorseQueue queue)> CreateQueue()
    {
        var (rider, port, server) = await PartitionTestServer.Create();
        string dataPath = $"pt-orphan-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
        var ctx = new PartitionTestContext(rider, port, dataPath, server);

        await rider.Queue.Create(QueueName, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                SubscribersPerPartition = 1,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 1,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                // Same as production. Keep it long: a short interval lets the partition be
                // destroyed mid-test once it drains, which silently turns this into a
                // different (and passing) scenario instead of the orphan one.
                AutoDestroyIdleSeconds = 120
            };
        });

        return (ctx, rider.Queue.Find(QueueName));
    }

    private static async Task<HorseClient> ConnectWorker(int port)
    {
        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        return client;
    }

    private static Task SubscribeNoLabel(HorseClient client)
        => client.Queue.Subscribe(QueueName, true, CancellationToken.None);

    private static Task PushLabeled(HorseClient producer, string label)
        => producer.Queue.Push(QueueName, Encoding.UTF8.GetBytes($"msg-{label}"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) }, CancellationToken.None);

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

    /// <summary>
    /// The production failure, reproduced: the worker that owned the tenant
    /// partition disconnects while the partition still has traffic, then a
    /// replacement worker subscribes. The replacement must take the partition
    /// over and drain it.
    /// </summary>
    [Fact]
    public async Task OrphanedPartition_IsReassigned_ToReplacementWorker()
    {
        var (ctx, queue) = await CreateQueue();
        await using var __ = ctx;

        HorseClient producer = await ConnectWorker(ctx.Port);

        // 1. First worker takes the tenant partition and drains it.
        HorseClient first = await ConnectWorker(ctx.Port);
        int firstReceived = 0;
        first.MessageReceived += (_, _) => Interlocked.Increment(ref firstReceived);

        await SubscribeNoLabel(first);
        await WaitUntil(() => queue.PartitionManager.AvailableWorkerCount > 0);

        await PushLabeled(producer, "tenant-1");
        await WaitUntil(() => firstReceived >= 1);
        Assert.Equal(1, firstReceived);
        Assert.Single(queue.PartitionManager.Partitions);

        // 2. The worker goes away (pod rollout). Wait until the SERVER has observed it:
        //    pushing while the departing client is still registered delivers the message to
        //    its dead socket (Acknowledge = None ⇒ fire and forget), which is a different
        //    defect and would mask the reassignment behaviour under test.
        PartitionEntry owned = queue.PartitionManager.Partitions.First();
        first.Disconnect();
        await WaitUntil(() => !first.IsConnected);
        await WaitUntil(() => !owned.Queue.Clients.Any());

        // 3. Traffic keeps arriving, so the partition still holds messages and
        //    AutoDestroy = NoMessages can never remove it.
        await PushLabeled(producer, "tenant-1");
        await WaitUntil(() => queue.PartitionManager.Partitions.Any());
        Assert.Single(queue.PartitionManager.Partitions);

        // 4. Replacement worker subscribes — it must inherit the partition.
        HorseClient replacement = await ConnectWorker(ctx.Port);
        int replacementReceived = 0;
        replacement.MessageReceived += (_, _) => Interlocked.Increment(ref replacementReceived);

        await SubscribeNoLabel(replacement);
        await WaitUntil(() => replacementReceived >= 1, 15000);

        PartitionEntry entry = queue.PartitionManager.Partitions.First();

        Assert.True(replacementReceived >= 1,
            $"replacement got nothing. partitionClients={entry.Queue.Clients.Count()} " +
            $"connectedClients={entry.Queue.Clients.Count(c => c.Client.IsConnected)} " +
            $"pool={queue.PartitionManager.AvailableWorkerCount} " +
            $"clientLimit={entry.Queue.Options.ClientLimit} " +
            $"pending={entry.Queue.Manager.MessageStore.Count()}");
        Assert.Equal("tenant-1", entry.Label);

        // The departed worker must not keep occupying the only subscriber slot.
        Assert.True(entry.Queue.Clients.All(c => c.Client.IsConnected),
            "A disconnected client is still occupying the partition's subscriber slot.");

        Assert.Equal(1, replacementReceived);
    }

    /// <summary>
    /// Same failure, but the partition is orphaned while it already holds a
    /// backlog: the worker disappears first and the replacement must both take
    /// the partition over and drain everything that queued up meanwhile.
    /// </summary>
    [Fact]
    public async Task OrphanedPartition_WithBacklog_IsDrained_ByReplacementWorker()
    {
        var (ctx, queue) = await CreateQueue();
        await using var __ = ctx;

        HorseClient producer = await ConnectWorker(ctx.Port);

        HorseClient first = await ConnectWorker(ctx.Port);
        int firstReceived = 0;
        first.MessageReceived += (_, _) => Interlocked.Increment(ref firstReceived);

        await SubscribeNoLabel(first);
        await WaitUntil(() => queue.PartitionManager.AvailableWorkerCount > 0);

        await PushLabeled(producer, "tenant-1");
        await WaitUntil(() => firstReceived >= 1);

        first.Disconnect();
        await WaitUntil(() => !first.IsConnected);

        // Backlog builds up while nobody is serving the partition.
        for (int i = 0; i < 5; i++)
            await PushLabeled(producer, "tenant-1");

        HorseClient replacement = await ConnectWorker(ctx.Port);
        int replacementReceived = 0;
        replacement.MessageReceived += (_, _) => Interlocked.Increment(ref replacementReceived);

        await SubscribeNoLabel(replacement);
        await WaitUntil(() => replacementReceived >= 5, 10000);

        Assert.Equal(5, replacementReceived);
    }
}
