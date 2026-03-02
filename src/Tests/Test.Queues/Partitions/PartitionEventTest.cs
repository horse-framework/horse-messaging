using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for QueuePartitionCreated event and IPartitionEventHandler lifecycle hooks.
/// </summary>
public class PartitionEventTest
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<(TestHorseRider server, int port, HorseQueue queue)> CreateQueue(string name = "ev-q")
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create(name, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.Disabled
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find(name);
        return (server, port, queue);
    }

    // ── Server-side handler ───────────────────────────────────────────────────

    private class TestPartitionEventHandler : IPartitionEventHandler
    {
        public int CreatedCount;
        public int DestroyedCount;
        public List<string> CreatedIds = new();

        public Task OnPartitionCreated(HorseQueue parentQueue, PartitionEntry partition)
        {
            Interlocked.Increment(ref CreatedCount);
            lock (CreatedIds) CreatedIds.Add(partition.PartitionId);
            return Task.CompletedTask;
        }

        public Task OnPartitionDestroyed(HorseQueue parentQueue, string partitionId)
        {
            Interlocked.Increment(ref DestroyedCount);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ServerHandler_CalledWhenPartitionCreated()
    {
        var (server, port, queue) = await CreateQueue();

        var handler = new TestPartitionEventHandler();
        server.Rider.Queue.PartitionEventHandlers.Add(handler);

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("ev-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "h1") });

        await Task.Delay(300);

        // Label partition = 1 created event
        Assert.True(handler.CreatedCount >= 1);

        server.Stop();
    }

    [Fact]
    public async Task ServerHandler_CalledForMultiplePartitions()
    {
        var (server, port, queue) = await CreateQueue();

        var handler = new TestPartitionEventHandler();
        server.Rider.Queue.PartitionEventHandlers.Add(handler);

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        await c1.ConnectAsync("horse://localhost:" + port);
        await c2.ConnectAsync("horse://localhost:" + port);
        await c1.Queue.Subscribe("ev-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl1") });
        await c2.Queue.Subscribe("ev-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl2") });

        await Task.Delay(300);

        // Two label partitions created
        Assert.True(handler.CreatedCount >= 2);

        server.Stop();
    }

    [Fact]
    public async Task ServerHandler_CalledWhenPartitionDestroyed()
    {
        var server = new TestHorseRider();
        await server.Initialize();

        await server.Rider.Queue.Create("dh-q", opts =>
        {
            opts.Type = QueueType.Push;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.NoConsumers,
                AutoDestroyIdleSeconds = 1
            };
        });

        int port = server.Start(300, 300);
        HorseQueue queue = server.Rider.Queue.Find("dh-q");

        var handler = new TestPartitionEventHandler();
        server.Rider.Queue.PartitionEventHandlers.Add(handler);

        HorseClient client = new HorseClient();
        await client.ConnectAsync("horse://localhost:" + port);
        await client.Queue.Subscribe("dh-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "kill-me") });

        await Task.Delay(200);
        client.Disconnect();

        await Task.Delay(3000); // wait for auto-destroy timer

        Assert.True(handler.DestroyedCount >= 1);

        server.Stop();
    }

    // ── Client-side HorseEventType.QueuePartitionCreated ─────────────────────

    [Fact]
    public async Task ClientEvent_QueuePartitionCreated_Fired()
    {
        var (server, port, queue) = await CreateQueue("client-ev-q");

        HorseClient subscriber = new HorseClient();
        subscriber.CatchEventMessages = true; // allow MessageReceived for Event type messages
        await subscriber.ConnectAsync("horse://localhost:" + port);

        int eventFired = 0;

        // MessageReceived fires for every message type including Event
        subscriber.MessageReceived += (_, msg) =>
        {
            if (msg.Type == MessageType.Event &&
                msg.ContentType == (ushort)HorseEventType.QueuePartitionCreated)
                Interlocked.Increment(ref eventFired);
        };

        // Subscribe to the server-side event
        await subscriber.Event.Subscribe(HorseEventType.QueuePartitionCreated, null, false);

        // Now create a partition by subscribing a worker
        HorseClient worker = new HorseClient();
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.Subscribe("client-ev-q", true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w-fire") });

        await Task.Delay(600);

        Assert.True(eventFired > 0);

        server.Stop();
    }

    // ── EventManager exposed ──────────────────────────────────────────────────

    [Fact]
    public async Task PartitionCreatedEvent_EventManagerExists()
    {
        var (server, _, _) = await CreateQueue();
        Assert.NotNull(server.Rider.Queue.PartitionCreatedEvent);
        server.Stop();
    }
}


