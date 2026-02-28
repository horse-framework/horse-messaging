using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Partitions;
using Horse.Server;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Server restart and consumer-reconnect tests for the partition system.
///
///  R1. Messages pushed while no consumer is connected are held in the .hdb file;
///      after a consumer re-subscribes they are delivered. (No restart — consumer bounce.)
///
///  R2. Server restart — parent queue options (PartitionOptions) survive.
///      After restart the parent queue comes back with Partition.Enabled=true.
///
///  R3. Messages pushed to a partition while consumer is offline (no restart) are
///      delivered when the consumer reconnects with the same label.
///
///  R4. Producer keeps pushing while consumer is disconnected; consumer reconnects
///      with the same label and receives all buffered messages.
///
///  R5. [Restart] Messages in a partition .hdb survive a full server restart;
///      a consumer reconnecting after restart receives them.
///      NOTE: Current implementation limitation — PartitionManager is not
///      reconstructed from queues.json on restart. The test documents this
///      known behaviour: the persisted sub-queue exists but is not yet
///      re-registered to the PartitionManager automatically.
/// </summary>
public class PartitionRestartTest : IDisposable
{
    private readonly List<string> _dataPaths = new();

    public void Dispose()
    {
        foreach (string path in _dataPaths)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a fresh HorseRider backed by PersistentQueues using the given dataPath.
    /// The path must exist or will be created by the rider.
    /// </summary>
    private HorseRider BuildRider(string dataPath) =>
        HorseRiderBuilder.Create()
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

    private static async Task<int> StartRider(HorseRider rider)
    {
        var rnd = new Random();
        for (int i = 0; i < 50; i++)
        {
            try
            {
                int port = rnd.Next(12000, 62000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;
                var s = new HorseServer(opts);
                s.UseRider(rider);
                s.StartAsync().GetAwaiter().GetResult();
                await Task.Delay(120);
                return port;
            }
            catch { Thread.Sleep(5); }
        }

        throw new InvalidOperationException("Could not bind any port after 50 attempts");
    }

    private static async Task<(HorseRider rider, int port, string dataPath)> CreateServer(
        string queueName,
        string dataPath = null,
        bool enableOrphan = false,
        int maxPartitions = 10,
        int subscribersPerPartition = 1)
    {
        // Reuse or generate a unique path
        bool newPath = dataPath == null;
        dataPath ??= $"pt-restart-{Environment.TickCount}-{new Random().Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
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

        int port = await StartRider(rider);

        // Only create the queue on a fresh data path (first boot)
        if (newPath && queueName != null)
        {
            await rider.Queue.Create(queueName, opts =>
            {
                opts.Type = QueueType.Push;
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    MaxPartitionCount = maxPartitions,
                    SubscribersPerPartition = subscribersPerPartition,
                    EnableOrphanPartition = enableOrphan,
                    AutoDestroy = PartitionAutoDestroy.Disabled
                };
            });
        }

        return (rider, port, dataPath);
    }

    // ─────────────────────────────────────────────────────────────────
    // R1. Consumer bounces (no restart) — offline-pushed messages delivered on reconnect
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Consumer subscribes with label → disconnects → producer pushes 3 messages →
    /// consumer reconnects with same label → receives all 3 messages.
    ///
    /// NEW BEHAVIOUR: Messages pushed while the labeled partition has no active
    /// consumer are stored in the labeled partition queue itself (not orphan).
    /// When the consumer reconnects it is subscribed to the same partition and
    /// Trigger() delivers the buffered messages.
    /// </summary>
    [Fact]
    public async Task ConsumerBounce_OfflinePushedMessages_DeliveredOnReconnect()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        // enableOrphan is irrelevant for this scenario now — labeled messages stay
        // in the labeled partition regardless of orphan setting.
        var (rider, port, dataPath) = await CreateServer($"bounce-{guid}", enableOrphan: false);
        _dataPaths.Add(dataPath);

        const string label = "tenant-bounce";
        string queueName = $"bounce-{guid}";

        // ── Phase 1: subscribe, then disconnect ──────────────────────
        HorseClient sub1 = new HorseClient { AutoAcknowledge = true };
        await sub1.ConnectAsync("horse://localhost:" + port);
        await sub1.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        HorseQueue parent = rider.Queue.Find(queueName);
        PartitionEntry entry = parent.PartitionManager.Partitions.First(p => p.Label == label);

        sub1.Disconnect();
        await Task.Delay(300);

        // ── Phase 2: push 3 messages while consumer is offline ───────
        HorseClient prod = new HorseClient();
        await prod.ConnectAsync("horse://localhost:" + port);

        for (int i = 0; i < 3; i++)
            await prod.Queue.Push(queueName, $"offline-{i}", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(500); // allow flush

        // Messages must be stored in the LABELED partition, not in orphan or dropped
        Assert.Equal(3, entry.Queue.Manager.MessageStore.Count());
        Assert.True(File.Exists(Path.Combine(dataPath, $"{entry.Queue.Name}.hdb")));

        // ── Phase 3: consumer reconnects, receives all 3 ─────────────
        int[] received = { 0 };
        HorseClient sub2 = new HorseClient { AutoAcknowledge = true };
        sub2.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await sub2.ConnectAsync("horse://localhost:" + port);
        await sub2.Queue.SubscribePartitioned(queueName, label, true);

        await Task.Delay(1000);
        Assert.Equal(3, received[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // R2. Server restart — parent queue PartitionOptions survive
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// After a server restart the parent queue is restored from queues.json
    /// with Partition.Enabled=true and the correct MaxPartitionCount.
    /// </summary>
    [Fact]
    public async Task ServerRestart_ParentQueue_PartitionOptionsRestored()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"restart-parent-{guid}";
        string dataPath;

        // ── Phase 1: create and verify ───────────────────────────────
        {
            var (rider1, _, dp) = await CreateServer(queueName, maxPartitions: 7);
            dataPath = dp;
            _dataPaths.Add(dataPath);

            HorseQueue q = rider1.Queue.Find(queueName);
            Assert.NotNull(q);
            Assert.True(q.Options.Partition.Enabled);
            Assert.Equal(7, q.Options.Partition.MaxPartitionCount);

            // Give queues.json time to be written
            await Task.Delay(200);
        }

        // ── Phase 2: rebuild rider from same dataPath ─────────────────
        {
            HorseRider rider2 = BuildRider(dataPath);
            await StartRider(rider2);

            // queues.json should restore the parent queue
            HorseQueue q2 = rider2.Queue.Find(queueName);
            Assert.NotNull(q2);
            Assert.True(q2.Options.Partition.Enabled, "Partition.Enabled must survive restart");
            Assert.Equal(7, q2.Options.Partition.MaxPartitionCount);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // R3. Producer keeps pushing while consumer is disconnected
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Producer continuously pushes to a labeled partition.
    /// Consumer starts, pauses (disconnects), then reconnects.
    /// Consumer must receive all messages — both before and after reconnect.
    /// </summary>
    [Fact]
    public async Task Producer_Continuous_ConsumerReconnects_ReceivesAll()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        var (rider, port, dataPath) = await CreateServer($"cont-{guid}", enableOrphan: false);
        _dataPaths.Add(dataPath);

        const string label = "worker-cont";
        string queueName = $"cont-{guid}";

        // ── Phase 1: consumer active, receives first batch ───────────
        int[] received = { 0 };
        HorseClient worker1 = new HorseClient { AutoAcknowledge = true };
        worker1.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await worker1.ConnectAsync("horse://localhost:" + port);
        await worker1.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        HorseClient prod = new HorseClient();
        await prod.ConnectAsync("horse://localhost:" + port);

        // Push 5 messages while connected
        for (int i = 0; i < 5; i++)
            await prod.Queue.Push(queueName, $"online-{i}", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(500);
        Assert.Equal(5, received[0]);

        // ── Phase 2: consumer drops, 4 more messages pushed ──────────
        HorseQueue parent = rider.Queue.Find(queueName);
        PartitionEntry part = parent.PartitionManager.Partitions.First(p => p.Label == label);

        worker1.Disconnect();
        await Task.Delay(200);

        for (int i = 0; i < 4; i++)
            await prod.Queue.Push(queueName, $"offline-{i}", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(500); // allow flush to disk

        // Offline messages must be stored in the LABELED partition (not dropped, not orphan)
        Assert.Equal(4, part.Queue.Manager.MessageStore.Count());

        // ── Phase 3: consumer reconnects with same label ──────────────
        int[] received2 = { 0 };
        HorseClient worker2 = new HorseClient { AutoAcknowledge = true };
        worker2.MessageReceived += (_, _) => Interlocked.Increment(ref received2[0]);
        await worker2.ConnectAsync("horse://localhost:" + port);
        await worker2.Queue.SubscribePartitioned(queueName, label, true);

        await Task.Delay(1000);
        Assert.Equal(4, received2[0]);
    }

    // ─────────────────────────────────────────────────────────────────
    // R4. Multiple labels — consumers bounce independently
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoTenants_ConsumerBounce_FullIsolationMaintained()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        var (rider, port, dataPath) = await CreateServer($"isolation-{guid}", enableOrphan: false);
        _dataPaths.Add(dataPath);

        string queueName = $"isolation-{guid}";
        const string labelA = "tenant-A";
        const string labelB = "tenant-B";

        // ── Both consumers subscribe ──────────────────────────────────
        int[] aCount = { 0 };
        HorseClient workerA = new HorseClient { AutoAcknowledge = true };
        workerA.MessageReceived += (_, _) => Interlocked.Increment(ref aCount[0]);
        await workerA.ConnectAsync("horse://localhost:" + port);
        await workerA.Queue.SubscribePartitioned(queueName, labelA, true);

        int[] bCount = { 0 };
        HorseClient workerB = new HorseClient { AutoAcknowledge = true };
        workerB.MessageReceived += (_, _) => Interlocked.Increment(ref bCount[0]);
        await workerB.ConnectAsync("horse://localhost:" + port);
        await workerB.Queue.SubscribePartitioned(queueName, labelB, true);
        await Task.Delay(400);

        HorseClient prod = new HorseClient();
        await prod.ConnectAsync("horse://localhost:" + port);

        // Both online: each message goes to its labeled partition
        for (int i = 0; i < 2; i++)
        {
            await prod.Queue.Push(queueName, $"a-online-{i}", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, labelA) });
            await prod.Queue.Push(queueName, $"b-online-{i}", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, labelB) });
        }

        await Task.Delay(600);
        Assert.Equal(2, aCount[0]);
        Assert.Equal(2, bCount[0]);

        // ── Tenant-A drops ────────────────────────────────────────────
        HorseQueue parent = rider.Queue.Find(queueName);
        PartitionEntry partA = parent.PartitionManager.Partitions.First(p => p.Label == labelA);

        workerA.Disconnect();
        await Task.Delay(200);

        // Push 3 messages labeled A → stored in A's partition, NOT delivered to B
        for (int i = 0; i < 3; i++)
            await prod.Queue.Push(queueName, $"a-offline-{i}", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, labelA) });

        // Push 1 more to B (B is still online, receives immediately)
        await prod.Queue.Push(queueName, "b-while-a-offline", false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, labelB) });

        await Task.Delay(600);

        // Worker-B receives only its own message, never A's buffered ones
        Assert.Equal(3, bCount[0]);    // 2 online + 1 extra = 3

        // A's 3 offline messages are buffered in A's partition (NOT dropped, NOT cross-delivered)
        Assert.Equal(3, partA.Queue.Manager.MessageStore.Count());

        // ── Tenant-A reconnects, receives exactly its 3 buffered messages ──
        int[] aCount2 = { 0 };
        HorseClient workerA2 = new HorseClient { AutoAcknowledge = true };
        workerA2.MessageReceived += (_, _) => Interlocked.Increment(ref aCount2[0]);
        await workerA2.ConnectAsync("horse://localhost:" + port);
        await workerA2.Queue.SubscribePartitioned(queueName, labelA, true);

        await Task.Delay(1000);
        Assert.Equal(3, aCount2[0]);   // receives exactly the 3 buffered messages
        Assert.Equal(3, bCount[0]);    // B unchanged — no cross-delivery
    }

    // ─────────────────────────────────────────────────────────────────
    // R5. Full server restart — partition sub-queue queues.json round-trip
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Documents current behaviour of the partition system across a full server restart:
    ///
    ///  - Partition sub-queues ARE saved to queues.json (proved by presence check).
    ///  - The parent queue IS restored with Partition.Enabled=true.
    ///  - Partition sub-queues ARE restored as standalone HorseQueue instances
    ///    (IsPartitionQueue=true) and their .hdb messages are reloaded.
    /// <summary>
    /// Verifies full restart recovery for the partition system:
    ///
    ///  - Partition sub-queues are saved to queues.json with SubPartition metadata.
    ///  - On restart, sub-queues are re-attached to the parent PartitionManager.
    ///  - IsPartitionQueue is set correctly after re-attach.
    ///  - Buffered messages survive restart and are delivered when the consumer reconnects.
    ///  - The consumer gets the SAME partition (same queue name) — no new GUID generated.
    /// </summary>
    [Fact]
    public async Task ServerRestart_PartitionSubQueues_ReAttachedAndMessagesDelivered()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"rsr-{guid}";
        string label = "tenant-rsr";
        string dataPath;
        string partQueueName;

        // ── Phase 1: subscribe, push while offline, then stop server ─
        {
            var (rider1, port1, dp) = await CreateServer(queueName, enableOrphan: false);
            dataPath = dp;
            _dataPaths.Add(dataPath);

            // Subscribe → creates labeled partition
            HorseClient sub1 = new HorseClient { AutoAcknowledge = false }; // hold messages
            await sub1.ConnectAsync("horse://localhost:" + port1);
            await sub1.Queue.SubscribePartitioned(queueName, label, true);
            await Task.Delay(300);

            HorseQueue p1 = rider1.Queue.Find(queueName);
            PartitionEntry e1 = p1.PartitionManager.Partitions.First(q => q.Label == label);
            partQueueName = e1.Queue.Name; // e.g. "rsr-xxxx-Partition-abc123"

            sub1.Disconnect();
            await Task.Delay(200);

            // Push while consumer offline → stored in labeled partition
            HorseClient prod = new HorseClient();
            await prod.ConnectAsync("horse://localhost:" + port1);
            await prod.Queue.Push(queueName, "restart-payload", false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });
            await Task.Delay(600); // flush

            Assert.Equal(1, e1.Queue.Manager.MessageStore.Count());

            // Verify queues.json contains the partition sub-queue with SubPartition data
            string queuesJson = Path.Combine(dataPath, "queues.json");
            Assert.True(File.Exists(queuesJson), "queues.json must exist");
            string jsonContent = await File.ReadAllTextAsync(queuesJson);
            Assert.Contains(partQueueName, jsonContent);
            Assert.Contains("SubPartition", jsonContent); // SubPartition metadata persisted
        }

        // ── Phase 2: rebuild rider from same dataPath ─────────────────
        {
            HorseRider rider2 = BuildRider(dataPath);
            int port2 = await StartRider(rider2);
            await Task.Delay(300);

            // Parent queue restored with Partition.Enabled=true
            HorseQueue parent2 = rider2.Queue.Find(queueName);
            Assert.NotNull(parent2);
            Assert.True(parent2.Options.Partition.Enabled);

            // Partition sub-queue IS re-attached to PartitionManager
            Assert.NotEmpty(parent2.PartitionManager.Partitions);

            PartitionEntry reAttached = parent2.PartitionManager.Partitions
                .FirstOrDefault(p => p.Label == label);
            Assert.NotNull(reAttached);

            // SAME queue name — no new GUID generated
            Assert.Equal(partQueueName, reAttached.Queue.Name);

            // IsPartitionQueue is true after re-attach
            Assert.True(reAttached.Queue.IsPartitionQueue);

            // The message from phase 1 IS in the restored partition's store
            Assert.Equal(1, reAttached.Queue.Manager.MessageStore.Count());

            // Consumer reconnects with same label → gets assigned to the SAME partition
            // → buffered message is delivered immediately via Trigger()
            int[] received = { 0 };
            HorseClient sub2 = new HorseClient { AutoAcknowledge = true };
            sub2.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
            await sub2.ConnectAsync("horse://localhost:" + port2);
            await sub2.Queue.SubscribePartitioned(queueName, label, true);
            await Task.Delay(1000);

            // Message delivered successfully after restart
            Assert.Equal(1, received[0]);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // R6. Orphan partition — offline push, consumer reconnects
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Label-less messages routed to the orphan partition are held in the .hdb store
    /// when no consumer is present. When a consumer reconnects (same or different client),
    /// the orphan messages are delivered.
    /// </summary>
    [Fact]
    public async Task OrphanPartition_ConsumerBounce_OfflineMessages_Delivered()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        var (rider, port, dataPath) = await CreateServer($"orphan-bounce-{guid}", enableOrphan: true);
        _dataPaths.Add(dataPath);
        string queueName = $"orphan-bounce-{guid}";

        // ── Phase 1: subscribe (creates orphan), disconnect ───────────
        HorseClient sub1 = new HorseClient { AutoAcknowledge = true };
        await sub1.ConnectAsync("horse://localhost:" + port);
        await sub1.Queue.SubscribePartitioned(queueName, "lbl", true);
        await Task.Delay(300);

        sub1.Disconnect();
        await Task.Delay(300);

        // ── Phase 2: push label-less messages → orphan ────────────────
        HorseClient prod = new HorseClient();
        await prod.ConnectAsync("horse://localhost:" + port);
        for (int i = 0; i < 3; i++)
            await prod.Queue.Push(queueName, $"orphan-{i}", false);

        await Task.Delay(500); // flush

        HorseQueue parent = rider.Queue.Find(queueName);
        PartitionEntry orphanEntry = parent.PartitionManager.OrphanPartition;
        Assert.NotNull(orphanEntry);
        Assert.Equal(3, orphanEntry.Queue.Manager.MessageStore.Count());

        // ── Phase 3: consumer reconnects, gets the orphan messages ─────
        int[] received = { 0 };
        HorseClient sub2 = new HorseClient { AutoAcknowledge = true };
        sub2.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await sub2.ConnectAsync("horse://localhost:" + port);
        await sub2.Queue.SubscribePartitioned(queueName, "lbl", true);

        await Task.Delay(1200);
        Assert.Equal(3, received[0]);
    }
}

