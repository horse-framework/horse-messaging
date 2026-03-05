using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Data;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Server;
using Xunit;

namespace Test.Queues.Core;

/// <summary>
/// Tests for selecting queue managers when multiple managers are registered on the same server.
/// Covers: named registration, Create API parameter, message header, and default fallback.
/// </summary>
public class QueueManagerSelectionTest
{
    #region Helpers

    private static byte[] Encode(string s) => Encoding.UTF8.GetBytes(s);

    private static async Task<(HorseRider rider, int port, HorseServer server, string dataPath)> CreateMultiManagerServer()
    {
        string dataPath = $"qt-multi-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.AutoQueueCreation = true;
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.Acknowledge = QueueAckDecision.None;

                // Register "Memory" named manager
                q.UseMemoryQueues("Memory");

                // Register "Persistent" named manager
                q.UsePersistentQueues("Persistent",
                    db => db.UseInstantFlush().SetAutoShrink(false));
            })
            .Build();

        int port = 0;
        HorseServer horseServer = null;

        for (int i = 0; i < 50; i++)
        {
            try
            {
                port = Random.Shared.Next(10000, 60000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;

                var server = new HorseServer(opts);
                server.UseRider(rider);
                server.StartAsync().GetAwaiter().GetResult();
                horseServer = server;
                break;
            }
            catch
            {
                Thread.Sleep(5);
                port = 0;
            }
        }

        await Task.Delay(100);
        return (rider, port, horseServer, dataPath);
    }

    private static async Task<HorseClient> ConnectClient(int port, string clientId = null)
    {
        var c = new HorseClient();
        if (clientId != null) c.ClientId = clientId;
        c.ResponseTimeout = TimeSpan.FromSeconds(10);
        await c.ConnectAsync($"horse://localhost:{port}");
        Assert.True(c.IsConnected);
        return c;
    }

    private static async Task Cleanup(HorseServer server, string dataPath)
    {
        try { await server.StopAsync(); }
        catch { }

        try
        {
            if (Directory.Exists(dataPath))
                Directory.Delete(dataPath, true);
        }
        catch { }
    }

    #endregion

    [Fact]
    public async Task MultipleManagers_DefaultFallback_UsesFirstRegistered()
    {
        // First registered manager ("Memory") becomes the "Default".
        // Queue created without specifying a manager should use Memory.
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);
            var result = await client.Queue.Create("q-default", o => o.Type = MessagingQueueType.Push, null, null, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            HorseQueue queue = rider.Queue.Find("q-default");
            Assert.NotNull(queue);
            Assert.IsType<MemoryQueueManager>(queue.Manager);

            client.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task CreateWithManagerName_Memory_UsesMemoryManager()
    {
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);
            var result = await client.Queue.Create("q-mem", o => o.Type = MessagingQueueType.Push, "Memory", null, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            HorseQueue queue = rider.Queue.Find("q-mem");
            Assert.NotNull(queue);
            Assert.IsType<MemoryQueueManager>(queue.Manager);

            client.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task CreateWithManagerName_Persistent_UsesPersistentManager()
    {
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);
            var result = await client.Queue.Create("q-persist", o => o.Type = MessagingQueueType.Push, "Persistent", null, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            HorseQueue queue = rider.Queue.Find("q-persist");
            Assert.NotNull(queue);
            Assert.IsType<PersistentQueueManager>(queue.Manager);

            client.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task PushWithManagerHeader_AutoCreatesWithSpecifiedManager()
    {
        // When a queue doesn't exist and AutoQueueCreation is enabled,
        // the QUEUE_MANAGER header on the push message determines the manager.
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);

            var headers = new List<KeyValuePair<string, string>>
            {
                new(HorseHeaders.QUEUE_MANAGER, "Persistent")
            };

            // Push to non-existent queue with Persistent header → should auto-create with Persistent manager
            await client.Queue.Push("q-header-persist", new MemoryStream(Encode("test")), false, headers, CancellationToken.None);
            await Task.Delay(500);

            HorseQueue queue = rider.Queue.Find("q-header-persist");
            Assert.NotNull(queue);
            Assert.IsType<PersistentQueueManager>(queue.Manager);

            client.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task PushWithMemoryManagerHeader_AutoCreatesWithMemoryManager()
    {
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);

            var headers = new List<KeyValuePair<string, string>>
            {
                new(HorseHeaders.QUEUE_MANAGER, "Memory")
            };

            await client.Queue.Push("q-header-mem", new MemoryStream(Encode("test")), false, headers, CancellationToken.None);
            await Task.Delay(500);

            HorseQueue queue = rider.Queue.Find("q-header-mem");
            Assert.NotNull(queue);
            Assert.IsType<MemoryQueueManager>(queue.Manager);

            client.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task TwoQueues_DifferentManagers_OnSameServer()
    {
        // Create two queues on the same server, each with a different manager.
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);

            await client.Queue.Create("q-fast", o => o.Type = MessagingQueueType.Push, "Memory", null, CancellationToken.None);
            await client.Queue.Create("q-durable", o => o.Type = MessagingQueueType.Push, "Persistent", null, CancellationToken.None);

            HorseQueue fast = rider.Queue.Find("q-fast");
            HorseQueue durable = rider.Queue.Find("q-durable");

            Assert.NotNull(fast);
            Assert.NotNull(durable);
            Assert.IsType<MemoryQueueManager>(fast.Manager);
            Assert.IsType<PersistentQueueManager>(durable.Manager);

            // Both queues should work independently
            int fastReceived = 0, durableReceived = 0;

            var consumer = await ConnectClient(port, "consumer");
            consumer.MessageReceived += (_, msg) =>
            {
                if (msg.Target == "q-fast") Interlocked.Increment(ref fastReceived);
                if (msg.Target == "q-durable") Interlocked.Increment(ref durableReceived);
            };
            await consumer.Queue.Subscribe("q-fast", true, CancellationToken.None);
            await consumer.Queue.Subscribe("q-durable", true, CancellationToken.None);

            await client.Queue.Push("q-fast", new MemoryStream(Encode("fast-msg")), false, CancellationToken.None);
            await client.Queue.Push("q-durable", new MemoryStream(Encode("durable-msg")), false, CancellationToken.None);

            for (int i = 0; i < 30 && (fastReceived < 1 || durableReceived < 1); i++)
                await Task.Delay(100);

            Assert.Equal(1, fastReceived);
            Assert.Equal(1, durableReceived);

            client.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task ManagerHeader_IgnoredAfterQueueExists()
    {
        // Once a queue is created with a manager, subsequent pushes with a different
        // QUEUE_MANAGER header should NOT change the manager.
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            var client = await ConnectClient(port);

            // Create queue with Memory manager
            await client.Queue.Create("q-fixed", o => o.Type = MessagingQueueType.Push, "Memory", null, CancellationToken.None);

            HorseQueue queue = rider.Queue.Find("q-fixed");
            Assert.IsType<MemoryQueueManager>(queue.Manager);

            // Push with Persistent header → manager should NOT change
            var headers = new List<KeyValuePair<string, string>>
            {
                new(HorseHeaders.QUEUE_MANAGER, "Persistent")
            };
            await client.Queue.Push("q-fixed", new MemoryStream(Encode("msg")), false, headers, CancellationToken.None);
            await Task.Delay(300);

            // Manager should still be Memory
            queue = rider.Queue.Find("q-fixed");
            Assert.IsType<MemoryQueueManager>(queue.Manager);

            client.Disconnect();
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }

    [Fact]
    public async Task GetQueueManagers_ReturnsAllRegistered()
    {
        var (rider, port, server, dataPath) = await CreateMultiManagerServer();
        try
        {
            string[] managers = rider.Queue.GetQueueManagers();

            // Should have at least: Memory, Persistent, Default (case may vary)
            Assert.Contains(managers, m => m.Equals("Memory", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(managers, m => m.Equals("Persistent", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(managers, m => m.Equals("Default", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await Cleanup(server, dataPath);
        }
    }
}

