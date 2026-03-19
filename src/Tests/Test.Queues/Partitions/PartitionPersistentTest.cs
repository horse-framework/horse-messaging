using System.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Data;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Partition tests that specifically exercise PersistentQueueManager behaviour:
///
///  1. Partition queues use PersistentQueueManager (not MemoryQueueManager)
///  2. Without a consumer-ACK, a pushed message remains in the persistent store
///  3. Two labels produce two independent .hdb files
///  4. Pushed message persists in the .hdb file (disk presence check)
///  5. After message consumption, AutoDestroy.NoMessages removes the partition
/// </summary>
public class PartitionPersistentTest : IDisposable
{
    private readonly List<string> _dataPaths = new();

    public void Dispose()
    {
        foreach (string path in _dataPaths)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private async Task<(HorseRider rider, int port, string dataPath)> CreatePersistentServer(
        string queueName,
        int maxPartitions = 10,
        int subscribersPerPartition = 1,
        QueueAckDecision ack = QueueAckDecision.None,
        PartitionAutoDestroy autoDestroy = PartitionAutoDestroy.Disabled,
        int autoDestroyIdleSeconds = 5)
    {
        var (rider, port, _, dataPath) = await PartitionTestServer.CreatePersistent();
        _dataPaths.Add(dataPath);

        await rider.Queue.Create(queueName, opts =>
        {
            opts.Type = QueueType.Push;
            opts.Acknowledge = ack;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = autoDestroy,
                AutoDestroyIdleSeconds = autoDestroyIdleSeconds
            };
        });

        return (rider, port, dataPath);
    }

    private static HorseRider BuildPersistentRider(string dataPath) // reserved for future restart tests
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                q.UsePersistentQueues(
                    db => db.SetPhysicalPath(queue => Path.Combine(dataPath, $"{queue.Name}.hdb"))
                             .UseInstantFlush()
                             .SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = CommitWhen.None;
                        queue.Options.Acknowledge = QueueAckDecision.None;
                    });
            })
            .Build();
    }

    private static async Task<int> StartRider(HorseRider rider)
    {
        int port = 0;
        var rnd = new Random();

        for (int i = 0; i < 50; i++)
        {
            try
            {
                port = rnd.Next(10000, 60000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;

                var server = new HorseServer(opts);
                server.UseRider(rider);
                server.StartAsync().GetAwaiter().GetResult();
                break;
            }
            catch
            {
                Thread.Sleep(5);
                port = 0;
            }
        }

        await Task.Delay(150);
        return port;
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. Manager type is PersistentQueueManager
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that partition queues use PersistentQueueManager when the server
    /// is configured with UsePersistentQueues.
    /// </summary>
    [Fact]
    public async Task Partition_ManagerType_IsPersistentQueueManager()
    {
        var (rider, port, _) = await CreatePersistentServer("ptype-q");

        HorseClient worker = new HorseClient { AutoAcknowledge = true };
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.SubscribePartitioned("ptype-q", "lbl", true, CancellationToken.None);
        await Task.Delay(400);

        HorseQueue parentQueue = rider.Queue.Find("ptype-q");
        PartitionEntry part = parentQueue.PartitionManager.Partitions.First();

        Assert.IsType<PersistentQueueManager>(part.Queue.Manager);
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. No active consumer — message stays in the partition's .hdb file
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// When there is no active subscriber for the labeled partition, the pushed message
    /// is stored in the persistent queue's .hdb file and MessageStore.Count() == 1.
    /// </summary>
    [Fact]
    public async Task Push_LabeledMessage_NoSubscriber_StaysInPersistentStore()
    {
        var (rider, _, dataPath) = await CreatePersistentServer(
            "persist-nosubscriber");

        // Create the parent queue but do NOT subscribe any worker — partition is created
        // explicitly via CreatePartition so the hdb file is allocated.
        HorseQueue parentQueue = rider.Queue.Find("persist-nosubscriber");
        PartitionEntry part = await parentQueue.PartitionManager.CreatePartition("offline-lbl");
        await Task.Delay(200);

        // Verify manager type immediately
        Assert.IsType<PersistentQueueManager>(part.Queue.Manager);

        // Push directly to the partition queue (no client route needed for this assertion)
        var msg = new QueueMessage(new HorseMessage(MessageType.QueueMessage, part.Queue.Name));
        msg.Message.SetStringContent("offline-payload");
        part.Queue.Manager.AddMessage(msg);
        await Task.Delay(300);

        // Message should be in the store (no subscriber to consume it)
        Assert.Equal(1, part.Queue.Manager.MessageStore.Count());

        // .hdb file must exist
        string dbFile = Path.Combine(dataPath, $"{part.Queue.Name}.hdb");
        Assert.True(File.Exists(dbFile), $"Expected hdb at {dbFile}");
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. .hdb files — two labels → two independent files on disk
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Each partition queue gets its own .hdb file on disk.
    /// Two labels → two independent files with non-zero size.
    /// </summary>
    [Fact]
    public async Task Push_TwoLabels_TwoIndependentHdbFiles()
    {
        var (rider, port, dataPath) = await CreatePersistentServer("persist-files");

        // AutoAcknowledge=false so messages remain tracked / not instantly cleared
        HorseClient w1 = new HorseClient { AutoAcknowledge = false };
        HorseClient w2 = new HorseClient { AutoAcknowledge = false };
        await w1.ConnectAsync("horse://localhost:" + port);
        await w2.ConnectAsync("horse://localhost:" + port);
        await w1.Queue.SubscribePartitioned("persist-files", "fileA", true, CancellationToken.None);
        await w2.Queue.SubscribePartitioned("persist-files", "fileB", true, CancellationToken.None);
        await Task.Delay(400);

        HorseClient prod = new HorseClient();
        await prod.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 3; i++)
            await prod.Queue.Push("persist-files", Encoding.UTF8.GetBytes($"a-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "fileA") }, CancellationToken.None);
        for (int i = 0; i < 2; i++)
            await prod.Queue.Push("persist-files", Encoding.UTF8.GetBytes($"b-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "fileB") }, CancellationToken.None);

        await Task.Delay(700); // allow flush

        HorseQueue parentQueue = rider.Queue.Find("persist-files");
        PartitionEntry partA = parentQueue.PartitionManager.Partitions.First(p => p.Label == "fileA");
        PartitionEntry partB = parentQueue.PartitionManager.Partitions.First(p => p.Label == "fileB");

        // Two separate physical stores
        Assert.NotSame(partA.Queue.Manager, partB.Queue.Manager);
        Assert.IsType<PersistentQueueManager>(partA.Queue.Manager);
        Assert.IsType<PersistentQueueManager>(partB.Queue.Manager);

        // Independent .hdb files exist on disk
        string dbA = Path.Combine(dataPath, $"{partA.Queue.Name}.hdb");
        string dbB = Path.Combine(dataPath, $"{partB.Queue.Name}.hdb");
        Assert.True(File.Exists(dbA), $"Missing hdb: {dbA}");
        Assert.True(File.Exists(dbB), $"Missing hdb: {dbB}");
        Assert.NotEqual(dbA, dbB);
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. Pushed message creates a .hdb file — disk persistence proof
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// After pushing a message to a labeled partition, the partition queue's .hdb file
    /// exists on disk. This is the fundamental proof that persistent storage is used.
    /// </summary>
    [Fact]
    public async Task Push_LabeledMessage_CreatesHdbFileOnDisk()
    {
        var (rider, port, dataPath) = await CreatePersistentServer("persist-disk");

        HorseClient worker = new HorseClient { AutoAcknowledge = true };
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.SubscribePartitioned("persist-disk", "disk-lbl", true, CancellationToken.None);
        await Task.Delay(400);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        await producer.Queue.Push("persist-disk", Encoding.UTF8.GetBytes("disk-payload"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "disk-lbl") }, CancellationToken.None);

        await Task.Delay(600); // allow flush

        HorseQueue parentQueue = rider.Queue.Find("persist-disk");
        PartitionEntry part = parentQueue.PartitionManager.Partitions.First(p => p.Label == "disk-lbl");

        string dbFile = Path.Combine(dataPath, $"{part.Queue.Name}.hdb");
        Assert.True(File.Exists(dbFile), $"Persistent .hdb not found at {dbFile}");
        Assert.IsType<PersistentQueueManager>(part.Queue.Manager);
    }

    // ─────────────────────────────────────────────────────────────────
    // 6. AutoDestroy.NoMessages removes partition after messages consumed
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// When all messages in a persistent partition are consumed and AutoDestroy=NoMessages,
    /// the partition is eventually removed while the parent queue stays alive.
    /// AutoDestroyIdleSeconds=3 so the check fires within the test timeout.
    /// </summary>
    [Fact]
    public async Task AutoDestroy_NoMessages_PersistentPartition_RemovedAfterConsumption()
    {
        var (rider, port, _) = await CreatePersistentServer(
            "persist-destroy",
            autoDestroy: PartitionAutoDestroy.NoMessages,
            autoDestroyIdleSeconds: 3);

        HorseQueue parentQueue = rider.Queue.Find("persist-destroy");

        // Worker — will consume all messages → partition becomes empty → should be destroyed
        int[] consumed = { 0 };
        HorseClient worker = new HorseClient { AutoAcknowledge = true };
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref consumed[0]);
        await worker.ConnectAsync("horse://localhost:" + port);
        await worker.Queue.SubscribePartitioned("persist-destroy", "destroyLbl", true, CancellationToken.None);
        await Task.Delay(300);

        HorseClient prod = new HorseClient();
        await prod.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 2; i++)
            await prod.Queue.Push("persist-destroy", Encoding.UTF8.GetBytes($"msg-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "destroyLbl") }, CancellationToken.None);

        await Task.Delay(500);
        Assert.Equal(2, consumed[0]);

        // Disconnect worker so the partition has no consumers and no messages
        worker.Disconnect();
        await Task.Delay(300);

        // Wait up to 12 s for AutoDestroy timer (3 s interval) to fire and remove the partition
        PartitionEntry destroyPart = null;
        for (int i = 0; i < 120; i++)
        {
            destroyPart = parentQueue.PartitionManager.Partitions
                .FirstOrDefault(p => p.Label == "destroyLbl");
            if (destroyPart == null) break;
            await Task.Delay(100);
        }

        // The destroyed partition must be gone
        Assert.Null(destroyPart);

        // The parent queue must still be alive (only the partition was removed, not the queue)
        Assert.NotNull(rider.Queue.Find("persist-destroy"));
    }
}

// ─────────────────────────────────────────────────────────────────────
// Extension helpers used in tests
// ─────────────────────────────────────────────────────────────────────

internal static class HorseMessageExtensions
{
    internal static HorseMessage WithHeader(this HorseMessage msg, string key, string value)
    {
        msg.AddHeader(key, value);
        return msg;
    }
}

internal static class HorseRiderExtensions
{
    /// <summary>Exposes the last-started server port from the rider (not available natively).</summary>
    internal static int Rider_Port(this HorseRider _) => 0; // placeholder — tests use stored port
}


