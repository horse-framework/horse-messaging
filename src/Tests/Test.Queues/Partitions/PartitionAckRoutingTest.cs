using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
/// Verifies that consumer ACK messages are routed to the correct partition
/// sub-queue — not to the parent queue.
///
/// Background (bug that these tests prevent from regressing):
///   When a message is routed from parent → partition sub-queue, the message's
///   Target header must be rewritten to the partition sub-queue name.  Otherwise
///   the consumer's ACK (whose Target is derived from the original message) goes
///   to the parent queue.  The parent queue's delivery tracker has no matching
///   delivery, so the ACK is silently lost.  The message stays in the HDB file
///   forever and is re-delivered on the next server restart.
///
/// All tests in this class use PersistentQueues with InstantFlush so that
///   HDB state can be inspected immediately after ACK processing.
/// </summary>
public class PartitionAckRoutingTest : IDisposable
{
    private readonly List<string> _dataPaths = new();

    public void Dispose()
    {
        foreach (string path in _dataPaths)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* best-effort */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private static async Task<(HorseRider rider, int port, string dataPath)> CreatePersistentServer(
        string queueName,
        QueueAckDecision ackDecision,
        QueueType queueType = QueueType.RoundRobin,
        int maxPartitions = 10,
        int subscribersPerPartition = 5)
    {
        string dataPath = $"pt-ack-{Environment.TickCount}-{new Random().Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = queueType;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.Acknowledge = ackDecision;
                q.Options.AutoQueueCreation = true;
                q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(5);

                q.UsePersistentQueues(
                    db => db.SetPhysicalPath(queue => Path.Combine(dataPath, $"{queue.Name}.hdb"))
                        .UseInstantFlush()
                        .SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = CommitWhen.AfterReceived;
                        queue.Options.Acknowledge = ackDecision;
                    });
            })
            .Build();

        int port = await StartRider(rider);

        await rider.Queue.Create(queueName, opts =>
        {
            opts.Type = queueType;
            opts.Acknowledge = ackDecision;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = maxPartitions,
                SubscribersPerPartition = subscribersPerPartition,
                AutoDestroy = PartitionAutoDestroy.Disabled,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 50
            };
        });

        return (rider, port, dataPath);
    }

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
            catch
            {
                Thread.Sleep(5);
            }
        }

        throw new InvalidOperationException("Could not bind any port after 50 attempts");
    }

    /// <summary>
    /// Returns the number of non-deleted (active) messages in the given HDB file.
    /// Parses the binary HDB format: 0x10 = Insert, 0x11 = Delete.
    /// </summary>
    private static int CountActiveMessagesInHdb(string hdbPath)
    {
        if (!File.Exists(hdbPath))
            return 0;

        byte[] data = File.ReadAllBytes(hdbPath);
        if (data.Length == 0)
            return 0;

        var inserts = new List<string>();
        var deletes = new HashSet<string>();
        int pos = 0;

        while (pos < data.Length)
        {
            byte dtype = data[pos++];
            if (dtype == 0x00 || pos >= data.Length) break;

            int idLen = data[pos++];
            if (pos + idLen > data.Length) break;
            string msgId = Encoding.UTF8.GetString(data, pos, idLen);
            pos += idLen;

            if (dtype == 0x11) // Delete
            {
                deletes.Add(msgId);
            }
            else if (dtype == 0x10) // Insert
            {
                if (pos + 4 > data.Length) break;
                int contentLen = BitConverter.ToInt32(data, pos);
                pos += 4 + contentLen;
                inserts.Add(msgId);
            }
            else
            {
                break;
            }
        }

        return inserts.Count(id => !deletes.Contains(id));
    }

    // ─────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// ACK routing with JustRequest: consumer sends ACK, message must be
    /// DEL-flagged in the partition sub-queue's HDB file (not the parent's).
    /// </summary>
    [Fact]
    public async Task JustRequest_AckDeletesMessageFromPartitionHdb()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-jr-{guid}";
        const string label = "tenant-ack-jr";

        var (rider, port, dataPath) = await CreatePersistentServer(queueName, QueueAckDecision.JustRequest);
        _dataPaths.Add(dataPath);

        // Subscribe with label
        HorseClient consumer = new HorseClient { AutoAcknowledge = true };
        await consumer.ConnectAsync("horse://localhost:" + port);
        await consumer.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        // Push a labeled message
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        int[] received = { 0 };
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);

        await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes("ack-test-payload"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        // Wait for consume + ack round-trip
        await Task.Delay(1500);

        Assert.Equal(1, received[0]);

        // Verify: partition HDB must have 0 active messages (insert + delete)
        string partitionHdb = Path.Combine(dataPath, $"{queueName}-Partition-{label}.hdb");
        int active = CountActiveMessagesInHdb(partitionHdb);
        Assert.Equal(0, active);

        // Verify: parent queue HDB must be empty (message never stored in parent)
        string parentHdb = Path.Combine(dataPath, $"{queueName}.hdb");
        int parentActive = CountActiveMessagesInHdb(parentHdb);
        Assert.Equal(0, parentActive);
    }

    /// <summary>
    /// ACK routing with WaitForAcknowledge: the stricter mode where the queue
    /// blocks until ACK arrives.  Same expectation — HDB must be clean after ACK.
    /// </summary>
    [Fact]
    public async Task WaitForAck_AckDeletesMessageFromPartitionHdb()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-wfa-{guid}";
        const string label = "tenant-ack-wfa";

        var (rider, port, dataPath) = await CreatePersistentServer(queueName, QueueAckDecision.WaitForAcknowledge);
        _dataPaths.Add(dataPath);

        HorseClient consumer = new HorseClient { AutoAcknowledge = true };
        await consumer.ConnectAsync("horse://localhost:" + port);
        await consumer.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        int[] received = { 0 };
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);

        await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes("wfa-payload"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(1500);

        Assert.Equal(1, received[0]);

        string partitionHdb = Path.Combine(dataPath, $"{queueName}-Partition-{label}.hdb");
        int active = CountActiveMessagesInHdb(partitionHdb);
        Assert.Equal(0, active);
    }

    /// <summary>
    /// Multiple messages across multiple labels — each partition's HDB must be
    /// independently cleaned after its consumer ACKs.
    /// </summary>
    [Fact]
    public async Task MultiLabel_EachPartitionHdbCleanAfterAck()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-ml-{guid}";

        var (rider, port, dataPath) = await CreatePersistentServer(queueName, QueueAckDecision.JustRequest);
        _dataPaths.Add(dataPath);

        const string labelA = "free";
        const string labelB = "premium";

        // Two consumers, one per label
        int[] countA = { 0 }, countB = { 0 };

        HorseClient consumerA = new HorseClient { AutoAcknowledge = true };
        consumerA.MessageReceived += (_, _) => Interlocked.Increment(ref countA[0]);
        await consumerA.ConnectAsync("horse://localhost:" + port);
        await consumerA.Queue.SubscribePartitioned(queueName, labelA, true);

        HorseClient consumerB = new HorseClient { AutoAcknowledge = true };
        consumerB.MessageReceived += (_, _) => Interlocked.Increment(ref countB[0]);
        await consumerB.ConnectAsync("horse://localhost:" + port);
        await consumerB.Queue.SubscribePartitioned(queueName, labelB, true);

        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);

        // Push 3 messages per label
        for (int i = 0; i < 3; i++)
        {
            await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes($"a-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, labelA) });
            await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes($"b-{i}"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, labelB) });
        }

        await Task.Delay(2000);

        Assert.Equal(3, countA[0]);
        Assert.Equal(3, countB[0]);

        // Both partition HDBs must be clean
        string hdbA = Path.Combine(dataPath, $"{queueName}-Partition-{labelA}.hdb");
        string hdbB = Path.Combine(dataPath, $"{queueName}-Partition-{labelB}.hdb");

        Assert.Equal(0, CountActiveMessagesInHdb(hdbA));
        Assert.Equal(0, CountActiveMessagesInHdb(hdbB));

        // Parent HDB must be empty
        string parentHdb = Path.Combine(dataPath, $"{queueName}.hdb");
        Assert.Equal(0, CountActiveMessagesInHdb(parentHdb));
    }

    /// <summary>
    /// Message Target header must be rewritten to partition sub-queue name.
    /// This verifies the fix directly: after consume, the received message's
    /// Target must be the partition sub-queue name, not the parent queue name.
    /// </summary>
    [Fact]
    public async Task MessageTargetIsRewrittenToPartitionSubQueueName()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-tgt-{guid}";
        const string label = "target-check";

        var (rider, port, dataPath) = await CreatePersistentServer(queueName, QueueAckDecision.JustRequest);
        _dataPaths.Add(dataPath);

        string[] receivedTarget = { null };
        HorseClient consumer = new HorseClient { AutoAcknowledge = true };
        consumer.MessageReceived += (_, msg) => receivedTarget[0] = msg.Target;
        await consumer.ConnectAsync("horse://localhost:" + port);
        await consumer.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes("target-test"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(1000);

        // The message received by consumer must have Target = partition sub-queue name
        string expectedTarget = $"{queueName}-Partition-{label}";
        Assert.Equal(expectedTarget, receivedTarget[0]);
    }

    /// <summary>
    /// Without ACK (QueueAckDecision.None), message should be deleted from HDB
    /// immediately after send — no ACK routing involved.
    /// This is the baseline: even if Target rewriting were broken, None-ack
    /// queues would still work.  The test documents this difference.
    /// </summary>
    [Fact]
    public async Task NoAck_MessageDeletedFromHdbImmediatelyAfterSend()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-none-{guid}";
        const string label = "no-ack-label";

        var (rider, port, dataPath) = await CreatePersistentServer(queueName, QueueAckDecision.None);
        _dataPaths.Add(dataPath);

        int[] received = { 0 };
        HorseClient consumer = new HorseClient();
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await consumer.ConnectAsync("horse://localhost:" + port);
        await consumer.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes("no-ack-payload"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(1000);
        Assert.Equal(1, received[0]);

        // With None ack, message is deleted on send — HDB should be clean
        string partitionHdb = Path.Combine(dataPath, $"{queueName}-Partition-{label}.hdb");
        Assert.Equal(0, CountActiveMessagesInHdb(partitionHdb));
    }

    /// <summary>
    /// Consumer does NOT send ACK (AutoAcknowledge = false, no manual ack).
    /// With JustRequest, the message should remain active in HDB because
    /// the server never receives the ACK → never calls DeleteMessage.
    /// This is the negative test: proves that the HDB check is meaningful.
    /// </summary>
    [Fact]
    public async Task NoAckSent_MessageRemainsActiveInPartitionHdb()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-noack-{guid}";
        const string label = "no-ack-sent";

        var (rider, port, dataPath) = await CreatePersistentServer(queueName, QueueAckDecision.JustRequest);
        _dataPaths.Add(dataPath);

        // Consumer with AutoAcknowledge = FALSE — will not send ack
        int[] received = { 0 };
        HorseClient consumer = new HorseClient { AutoAcknowledge = false };
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await consumer.ConnectAsync("horse://localhost:" + port);
        await consumer.Queue.SubscribePartitioned(queueName, label, true);
        await Task.Delay(300);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes("pending-payload"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(1500);
        Assert.Equal(1, received[0]);

        // Message delivered but NOT acked → must remain active in HDB
        string partitionHdb = Path.Combine(dataPath, $"{queueName}-Partition-{label}.hdb");
        int active = CountActiveMessagesInHdb(partitionHdb);
        Assert.True(active > 0, "Message must remain active in HDB when consumer does not ACK");
    }

    /// <summary>
    /// Second run simulation: push + ack → verify HDB clean → restart server →
    /// consumer subscribes again → no stale messages delivered.
    /// This is the end-to-end regression test for the original bug.
    /// </summary>
    [Fact]
    public async Task ServerRestart_NoStaleMessagesAfterAck()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-restart-{guid}";
        const string label = "restart-ack";
        string dataPath;

        // ── Phase 1: push, consume, ack ──────────────────────────────
        {
            var (rider1, port1, dp) = await CreatePersistentServer(queueName, QueueAckDecision.JustRequest);
            dataPath = dp;
            _dataPaths.Add(dataPath);

            int[] received1 = { 0 };
            HorseClient consumer1 = new HorseClient { AutoAcknowledge = true };
            consumer1.MessageReceived += (_, _) => Interlocked.Increment(ref received1[0]);
            await consumer1.ConnectAsync("horse://localhost:" + port1);
            await consumer1.Queue.SubscribePartitioned(queueName, label, true);
            await Task.Delay(300);

            HorseClient producer1 = new HorseClient();
            await producer1.ConnectAsync("horse://localhost:" + port1);
            await producer1.Queue.Push(queueName, Encoding.UTF8.GetBytes("phase1-msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

            await Task.Delay(1500);
            Assert.Equal(1, received1[0]);

            // HDB must be clean
            string partHdb = Path.Combine(dataPath, $"{queueName}-Partition-{label}.hdb");
            Assert.Equal(0, CountActiveMessagesInHdb(partHdb));
        }

        // ── Phase 2: restart, subscribe again → no stale delivery ────
        {
            HorseRider rider2 = HorseRiderBuilder.Create()
                .ConfigureOptions(o => o.DataPath = dataPath)
                .ConfigureQueues(q =>
                {
                    q.Options.Type = QueueType.RoundRobin;
                    q.Options.CommitWhen = CommitWhen.AfterReceived;
                    q.Options.Acknowledge = QueueAckDecision.JustRequest;
                    q.Options.AutoQueueCreation = true;
                    q.Options.AcknowledgeTimeout = TimeSpan.FromSeconds(5);

                    q.UsePersistentQueues(
                        db => db.SetPhysicalPath(queue => Path.Combine(dataPath, $"{queue.Name}.hdb"))
                            .UseInstantFlush()
                            .SetAutoShrink(false),
                        queue =>
                        {
                            queue.Options.CommitWhen = CommitWhen.AfterReceived;
                            queue.Options.Acknowledge = QueueAckDecision.JustRequest;
                        });
                })
                .Build();

            int port2 = await StartRider(rider2);
            await Task.Delay(300);

            int[] received2 = { 0 };
            HorseClient consumer2 = new HorseClient { AutoAcknowledge = true };
            consumer2.MessageReceived += (_, _) => Interlocked.Increment(ref received2[0]);
            await consumer2.ConnectAsync("horse://localhost:" + port2);
            await consumer2.Queue.SubscribePartitioned(queueName, label, true);

            // Wait long enough — if stale messages exist they'd be delivered quickly
            await Task.Delay(2000);

            // No stale messages — received count must be 0
            Assert.Equal(0, received2[0]);
        }
    }

    /// <summary>
    /// Label-less partition with AutoAssignWorkers: worker from pool is assigned,
    /// message is consumed, ACK routes to the correct partition sub-queue.
    /// </summary>
    [Fact]
    public async Task LabelLess_AutoAssignWorker_AckRoutesToPartitionHdb()
    {
        string guid = Guid.NewGuid().ToString("N")[..8];
        string queueName = $"ack-ll-{guid}";

        var (rider, port, dataPath) = await CreatePersistentServer(
            queueName, QueueAckDecision.JustRequest, QueueType.RoundRobin, maxPartitions: 5, subscribersPerPartition: 5);
        _dataPaths.Add(dataPath);

        // Subscribe without label → goes to worker pool
        int[] received = { 0 };
        HorseClient consumer = new HorseClient { AutoAcknowledge = true };
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received[0]);
        await consumer.ConnectAsync("horse://localhost:" + port);
        await consumer.Queue.SubscribePartitioned(queueName, null, true);
        await Task.Delay(300);

        // Push with label → partition created, worker assigned from pool
        const string label = "dynamic-tenant";
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync("horse://localhost:" + port);
        await producer.Queue.Push(queueName, Encoding.UTF8.GetBytes("ll-payload"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) });

        await Task.Delay(1500);
        Assert.Equal(1, received[0]);

        // Partition HDB must be clean after ack
        string partHdb = Path.Combine(dataPath, $"{queueName}-Partition-{label}.hdb");
        Assert.Equal(0, CountActiveMessagesInHdb(partHdb));
    }
}

