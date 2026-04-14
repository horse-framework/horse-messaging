using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Partitions;
using Test.Common;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for QueuePartitionCreated event and IPartitionEventHandler lifecycle hooks.
/// </summary>
public class PartitionEventTest
{
    private static async Task RunWithQueue(
        Func<TestHorseRider, int, HorseQueue, Task> action,
        string name = "ev-q")
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
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

            HorseQueue queue = server.Rider.Queue.Find(name);
            await action(server, port, queue);
        });
    }

    private class TestPartitionEventHandler : IPartitionEventHandler
    {
        public int CreatedCount;
        public int DestroyedCount;
        public List<string> CreatedIds = new();

        public Task OnPartitionCreated(HorseQueue parentQueue, PartitionEntry partition)
        {
            Interlocked.Increment(ref CreatedCount);
            lock (CreatedIds)
                CreatedIds.Add(partition.PartitionId);
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
        await RunWithQueue(async (server, port, _) =>
        {
            var handler = new TestPartitionEventHandler();
            server.Rider.Queue.PartitionEventHandlers.Add(handler);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("ev-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "h1") }, CancellationToken.None);

            await Task.Delay(300);

            Assert.True(handler.CreatedCount >= 1);
        });
    }

    [Fact]
    public async Task ServerHandler_CalledForMultiplePartitions()
    {
        await RunWithQueue(async (server, port, _) =>
        {
            var handler = new TestPartitionEventHandler();
            server.Rider.Queue.PartitionEventHandlers.Add(handler);

            HorseClient c1 = new HorseClient();
            HorseClient c2 = new HorseClient();
            await c1.ConnectAsync("horse://localhost:" + port);
            await c2.ConnectAsync("horse://localhost:" + port);
            await c1.Queue.Subscribe("ev-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl1") }, CancellationToken.None);
            await c2.Queue.Subscribe("ev-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl2") }, CancellationToken.None);

            await Task.Delay(300);

            Assert.True(handler.CreatedCount >= 2);
        });
    }

    [Fact]
    public async Task ServerHandler_CalledWhenPartitionDestroyed()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
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

            HorseQueue queue = server.Rider.Queue.Find("dh-q");
            Assert.NotNull(queue);

            var handler = new TestPartitionEventHandler();
            server.Rider.Queue.PartitionEventHandlers.Add(handler);

            HorseClient client = new HorseClient();
            await client.ConnectAsync("horse://localhost:" + port);
            await client.Queue.Subscribe("dh-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "kill-me") }, CancellationToken.None);

            await Task.Delay(200);
            client.Disconnect();

            await Task.Delay(3000);

            Assert.True(handler.DestroyedCount >= 1);
        });
    }

    [Fact]
    public async Task ClientEvent_QueuePartitionCreated_Fired()
    {
        await RunWithQueue(async (_, port, _) =>
        {
            HorseClient subscriber = new HorseClient();
            subscriber.CatchEventMessages = true;
            await subscriber.ConnectAsync("horse://localhost:" + port);

            int eventFired = 0;
            subscriber.MessageReceived += (_, msg) =>
            {
                if (msg.Type == MessageType.Event &&
                    msg.ContentType == (ushort)HorseEventType.QueuePartitionCreated)
                    Interlocked.Increment(ref eventFired);
            };

            await subscriber.Event.Subscribe(HorseEventType.QueuePartitionCreated, null, false, CancellationToken.None);

            HorseClient worker = new HorseClient();
            await worker.ConnectAsync("horse://localhost:" + port);
            await worker.Queue.Subscribe("client-ev-q", true,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "w-fire") }, CancellationToken.None);

            await Task.Delay(600);

            Assert.True(eventFired > 0);
        }, "client-ev-q");
    }

    [Fact]
    public async Task PartitionCreatedEvent_EventManagerExists()
    {
        await RunWithQueue(async (server, _, _) =>
        {
            Assert.NotNull(server.Rider.Queue.PartitionCreatedEvent);
            await Task.CompletedTask;
        });
    }
}
