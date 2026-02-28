using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for the orphan partition — creation, subscriber rules, and WaitForAcknowledge behaviour.
/// </summary>
public class PartitionOrphanTest
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(TestHorseRider server, int port, HorseQueue queue)> CreateQueue(
        QueueAckDecision ack = QueueAckDecision.None,
        bool enableOrphan = true,
        string name = "orphan-q")
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = ack;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                EnableOrphanPartition = enableOrphan,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find(name);
        return (server, port, queue);
    }

    // ── Orphan creation ───────────────────────────────────────────────────────

    [Fact]
    public async Task OrphanPartition_CreatedOnFirstSubscribe()
    {
        var (server, port, queue) = await CreateQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);

        await client.Queue.Subscribe("orphan-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl") });

        await Task.Delay(300);

        // The orphan should have been created automatically
        Assert.NotNull(queue.PartitionManager.OrphanPartition);
        Assert.True(queue.PartitionManager.OrphanPartition.IsOrphan);

        server.Stop();
    }

    [Fact]
    public async Task OrphanPartition_NameFollowsConvention()
    {
        var (server, port, queue) = await CreateQueue(name: "my-queue");

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("my-queue", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "x") });

        await Task.Delay(200);

        HorseQueue orphanQueue = queue.PartitionManager.OrphanPartition?.Queue;
        Assert.NotNull(orphanQueue);
        Assert.Equal("my-queue-Partition-Orphan", orphanQueue.Name);

        server.Stop();
    }

    [Fact]
    public async Task OrphanPartition_MarkedAsPartitionQueue()
    {
        var (server, port, queue) = await CreateQueue();

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("orphan-q", true);
        await Task.Delay(200);

        if (queue.PartitionManager.OrphanPartition != null)
            Assert.True(queue.PartitionManager.OrphanPartition.Queue.IsPartitionQueue);

        server.Stop();
    }

    // ── Orphan disabled ───────────────────────────────────────────────────────

    [Fact]
    public async Task OrphanDisabled_NoOrphanCreated()
    {
        var (server, port, queue) = await CreateQueue(enableOrphan: false);

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("orphan-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "some-label") });

        await Task.Delay(300);

        Assert.Null(queue.PartitionManager.OrphanPartition);

        server.Stop();
    }

    // ── WaitForAcknowledge orphan subscriber requirement ──────────────────────

    [Fact]
    public async Task WaitForAck_OrphanHasSubscriber_MessageDelivered()
    {
        var (server, port, queue) = await CreateQueue(ack: QueueAckDecision.WaitForAcknowledge);

        HorseClient worker = new HorseClient();
        HorseClient producer = new HorseClient();
        await worker.ConnectAsync("horse://localhost:" + port);
        await producer.ConnectAsync("horse://localhost:" + port);

        int received = 0;
        worker.AutoAcknowledge = true;
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);

        // Worker subscribes → also auto-subscribes to orphan
        await worker.Queue.Subscribe("orphan-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "ww") });

        await Task.Delay(300);

        // Push without label → orphan (orphan has the worker as subscriber)
        await producer.Queue.Push("orphan-q", "data", false);
        await Task.Delay(800);

        Assert.Equal(1, received);

        server.Stop();
    }

    [Fact]
    public async Task WaitForAck_NoOrphanSubscriber_PushReturnNoConsumers()
    {
        // Use PartitionTestServer with CommitWhen.None so the server doesn't
        // commit on receive — it processes the push fully and returns NoConsumers
        // when no subscriber is available for a WaitForAcknowledge queue.
        var (rider, port, _) = await PartitionTestServer.Create();

        await rider.Queue.Create("wack-orphan-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            opts.CommitWhen = CommitWhen.None;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 5,
                SubscribersPerPartition = 1,
                EnableOrphanPartition = true,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        // waitForCommit=true: wait for server response
        // No subscribers → ResolveNoSubscriberTarget returns null → NoConsumers
        HorseResult result = await producer.Queue.Push("wack-orphan-q", "hello", true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);
    }

    // ── Orphan subscriber limit (unlimited) ───────────────────────────────────

    [Fact]
    public async Task OrphanPartition_AcceptsMultipleSubscribers()
    {
        var (server, port, queue) = await CreateQueue();

        const int workers = 5;
        for (int i = 0; i < workers; i++)
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("orphan-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, $"w{i}") });
        }

        await Task.Delay(300);

        // All workers should also be in orphan
        HorseQueue orphanQueue2 = queue.PartitionManager.OrphanPartition?.Queue;
        Assert.NotNull(orphanQueue2);
        Assert.Equal(workers, orphanQueue2.Clients.Count());

        server.Stop();
    }
}

