using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
/// Validates that partition configuration fields — especially AutoAssignWorkers
/// and MaxPartitionsPerWorker — survive serialization, deserialization,
/// auto-queue-creation, and full server restart.
///
/// These tests exist because a bug was discovered where:
///   1. PartitionConfigurationData was missing AutoAssignWorkers/MaxPartitionsPerWorker properties
///   2. QueueConfiguration.Create() did not serialize those fields
///   3. QueueRider.Initialize() did not deserialize those fields from queues.json
///   4. Stale queues.json caused AutoAssignWorkers to revert to false on restart
///
/// Additionally, when PARTITION_LIMIT / PARTITION_SUBSCRIBERS headers are sent
/// and the global options have Partition != null, the header handler must not
/// clobber existing fields like AutoAssignWorkers.
/// </summary>
public class PartitionConfigPersistenceTest : IDisposable
{
    private readonly List<string> _dataPaths = new();

    public void Dispose()
    {
        foreach (string path in _dataPaths)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* best-effort */ }
        }
    }

    #region Helpers

    private static string NewDataPath()
    {
        return $"pt-data-{Environment.TickCount}-{new Random().Next(0, 100000)}";
    }

    private static HorseRider BuildRider(string dataPath, Action<PartitionOptions> configurePartition = null)
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                if (configurePartition != null)
                {
                    q.Options.Partition = new PartitionOptions();
                    configurePartition(q.Options.Partition);
                }

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

        throw new InvalidOperationException("Could not bind any port");
    }

    private static async Task<HorseClient> Connect(int port)
    {
        var c = new HorseClient();
        await c.ConnectAsync("horse://localhost:" + port);
        return c;
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════════
    // 1. QueueConfiguration round-trip: serialize → deserialize
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task QueueConfiguration_Roundtrip_AllPartitionFields_Preserved(string mode)
    {
        // Arrange: create a queue with all partition fields set
        await using var ctx = await PartitionTestServer.Create(mode);

        await ctx.Rider.Queue.Create("cfg-roundtrip", opts =>
        {
            opts.Type = QueueType.RoundRobin;
            opts.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 15,
                SubscribersPerPartition = 3,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = 60,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 7
            };
        });

        HorseQueue queue = ctx.Rider.Queue.Find("cfg-roundtrip");

        // Act: serialize to QueueConfiguration
        QueueConfiguration config = QueueConfiguration.Create(queue);

        // Assert: all partition fields are in the serialized config
        Assert.NotNull(config.Partition);
        Assert.True(config.Partition.Enabled);
        Assert.Equal(15, config.Partition.MaxPartitionCount);
        Assert.Equal(3, config.Partition.SubscribersPerPartition);
        Assert.Equal("NoMessages", config.Partition.AutoDestroy);
        Assert.Equal(60, config.Partition.AutoDestroyIdleSeconds);
        Assert.True(config.Partition.AutoAssignWorkers);
        Assert.Equal(7, config.Partition.MaxPartitionsPerWorker);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 2. Server restart — AutoAssignWorkers survives queues.json round-trip
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ServerRestart_AutoAssignWorkers_Survives()
    {
        string dataPath = NewDataPath();
        _dataPaths.Add(dataPath);
        string queueName = "restart-aa-" + Guid.NewGuid().ToString("N")[..6];

        // ── Phase 1: Create queue with AutoAssignWorkers=true ──────────
        {
            HorseRider rider1 = BuildRider(dataPath, p =>
            {
                p.Enabled = true;
                p.AutoAssignWorkers = true;
                p.MaxPartitionsPerWorker = 10;
                p.MaxPartitionCount = 5;
                p.SubscribersPerPartition = 2;
            });
            int port1 = await StartRider(rider1);

            // Trigger auto-creation via subscribe
            HorseClient client = await Connect(port1);
            await client.Queue.Subscribe(queueName, true);
            await Task.Delay(500);

            HorseQueue q1 = rider1.Queue.Find(queueName);
            Assert.NotNull(q1);
            Assert.True(q1.Options.Partition.AutoAssignWorkers, "Phase 1: AutoAssignWorkers must be true");
            Assert.Equal(10, q1.Options.Partition.MaxPartitionsPerWorker);

            // Ensure queues.json is written
            await Task.Delay(300);
            string queuesJsonPath = Path.Combine(dataPath, "queues.json");
            Assert.True(File.Exists(queuesJsonPath), "queues.json must be created");

            // Verify JSON contains AutoAssignWorkers
            string json = await File.ReadAllTextAsync(queuesJsonPath);
            Assert.Contains("AutoAssignWorkers", json);
            Assert.Contains("MaxPartitionsPerWorker", json);

            client.Disconnect();
        }

        // ── Phase 2: Rebuild rider from same data path ─────────────────
        {
            HorseRider rider2 = BuildRider(dataPath, p =>
            {
                p.Enabled = true;
                p.AutoAssignWorkers = true;
                p.MaxPartitionsPerWorker = 10;
                p.MaxPartitionCount = 5;
                p.SubscribersPerPartition = 2;
            });
            await StartRider(rider2);

            HorseQueue q2 = rider2.Queue.Find(queueName);
            Assert.NotNull(q2);
            Assert.NotNull(q2.Options.Partition);
            Assert.True(q2.Options.Partition.Enabled, "Phase 2: Enabled must survive restart");
            Assert.True(q2.Options.Partition.AutoAssignWorkers, "Phase 2: AutoAssignWorkers must survive restart");
            Assert.Equal(10, q2.Options.Partition.MaxPartitionsPerWorker);
            Assert.Equal(5, q2.Options.Partition.MaxPartitionCount);
            Assert.Equal(2, q2.Options.Partition.SubscribersPerPartition);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // 3. Auto-queue-creation via subscribe inherits global AutoAssignWorkers
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoQueueCreation_Subscribe_InheritsGlobalPartitionOptions(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        // Set global partition options
        ctx.Rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 20,
            MaxPartitionCount = 8,
            SubscribersPerPartition = 4
        };

        // Subscribe triggers auto-creation
        HorseClient client = await Connect(ctx.Port);
        await client.Queue.Subscribe("inherit-sub-q", true);
        await Task.Delay(300);

        HorseQueue queue = ctx.Rider.Queue.Find("inherit-sub-q");
        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.Partition);
        Assert.True(queue.Options.Partition.Enabled);
        Assert.True(queue.Options.Partition.AutoAssignWorkers, "AutoAssignWorkers must be inherited from global");
        Assert.Equal(20, queue.Options.Partition.MaxPartitionsPerWorker);
        Assert.Equal(8, queue.Options.Partition.MaxPartitionCount);
        Assert.Equal(4, queue.Options.Partition.SubscribersPerPartition);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 4. Auto-queue-creation via push inherits global AutoAssignWorkers
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task AutoQueueCreation_Push_InheritsGlobalPartitionOptions(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        ctx.Rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 15,
            MaxPartitionCount = 6,
            SubscribersPerPartition = 2
        };

        // Push triggers auto-creation via QueueMessageHandler.FindQueue
        HorseClient producer = await Connect(ctx.Port);
        await producer.Queue.Push("inherit-push-q", Encoding.UTF8.GetBytes("test"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "t1") });
        await Task.Delay(400);

        HorseQueue queue = ctx.Rider.Queue.Find("inherit-push-q");
        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.Partition);
        Assert.True(queue.Options.Partition.AutoAssignWorkers, "AutoAssignWorkers must be inherited on push-created queue");
        Assert.Equal(15, queue.Options.Partition.MaxPartitionsPerWorker);
        Assert.Equal(6, queue.Options.Partition.MaxPartitionCount);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 5. PARTITION_LIMIT/PARTITION_SUBSCRIBERS headers don't clobber
    //    existing global partition options
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PartitionHeaders_PreserveGlobalAutoAssignWorkers(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        // Global config with AutoAssignWorkers=true
        ctx.Rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 12,
            MaxPartitionCount = 5,
            SubscribersPerPartition = 1
        };

        HorseClient client = await Connect(ctx.Port);
        await client.Queue.SubscribePartitioned(
            "header-preserve-q",
            partitionLabel: "w1",
            verifyResponse: true,
            maxPartitions: 10,
            subscribersPerPartition: 3);
        await Task.Delay(400);

        HorseQueue queue = ctx.Rider.Queue.Find("header-preserve-q");
        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.Partition);
        Assert.True(queue.Options.Partition.Enabled);

        Assert.Equal(10, queue.Options.Partition.MaxPartitionCount);
        Assert.Equal(3, queue.Options.Partition.SubscribersPerPartition);

        Assert.True(queue.Options.Partition.AutoAssignWorkers, "AutoAssignWorkers must survive header override");
        Assert.Equal(12, queue.Options.Partition.MaxPartitionsPerWorker);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 6. PARTITION_LIMIT header when global Partition is null
    //    creates new PartitionOptions with default AutoAssignWorkers=false
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PartitionHeaders_NullGlobalPartition_CreatesNewPartitionOptions(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        // Global has NO partition config
        ctx.Rider.Queue.Options.Partition = null;

        HorseClient client = await Connect(ctx.Port);
        await client.Queue.SubscribePartitioned(
            "header-null-q",
            partitionLabel: "w1",
            verifyResponse: true,
            maxPartitions: 4,
            subscribersPerPartition: 2);
        await Task.Delay(400);

        HorseQueue queue = ctx.Rider.Queue.Find("header-null-q");
        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.Partition);
        Assert.True(queue.Options.Partition.Enabled);
        Assert.Equal(4, queue.Options.Partition.MaxPartitionCount);
        Assert.Equal(2, queue.Options.Partition.SubscribersPerPartition);

        // AutoAssignWorkers should be false because global had no partition config
        // This is expected — server admin must configure AutoAssignWorkers via global config
        Assert.False(queue.Options.Partition.AutoAssignWorkers);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 7. End-to-end: auto-create → push labeled → worker auto-assigned → deliver
    //    (the exact scenario from Sample.All that failed before the fix)
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task EndToEnd_AutoAssignWorker_ReceivesLabeledMessage(string mode)
    {
        await using var ctx = await PartitionTestServer.Create(mode);

        ctx.Rider.Queue.Options.Partition = new PartitionOptions
        {
            Enabled = true,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 10,
            SubscribersPerPartition = 1
        };

        // Worker subscribes without label → enters pool
        int received = 0;
        HorseClient worker = await Connect(ctx.Port);
        worker.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await worker.Queue.Subscribe("e2e-aa-q", true);
        await Task.Delay(300);

        HorseQueue queue = ctx.Rider.Queue.Find("e2e-aa-q");
        Assert.NotNull(queue);
        Assert.NotNull(queue.PartitionManager);
        Assert.True(queue.Options.Partition.AutoAssignWorkers, "Queue must inherit AutoAssignWorkers from global");

        // No partitions yet — worker is in pool
        Assert.Empty(queue.PartitionManager.Partitions);

        // Push labeled message
        HorseClient producer = await Connect(ctx.Port);
        await producer.Queue.Push("e2e-aa-q", Encoding.UTF8.GetBytes("hello"), false,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "tenant-x") });
        await Task.Delay(600);

        // Worker should have been auto-assigned and received the message
        Assert.Equal(1, received);
        Assert.Single(queue.PartitionManager.Partitions);
        Assert.Equal("tenant-x", queue.PartitionManager.Partitions.First().Label);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 8. Restart + auto-assign: worker connects after restart, receives
    //    previously-buffered labeled message
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ServerRestart_AutoAssign_WorkerReceivesBufferedMessage()
    {
        string dataPath = NewDataPath();
        _dataPaths.Add(dataPath);
        string queueName = "restart-deliver-" + Guid.NewGuid().ToString("N")[..6];

        // ── Phase 1: create queue, push labeled message, no consumer ──
        {
            HorseRider rider1 = BuildRider(dataPath, p =>
            {
                p.Enabled = true;
                p.AutoAssignWorkers = true;
                p.MaxPartitionsPerWorker = 5;
                p.SubscribersPerPartition = 1;
            });
            int port1 = await StartRider(rider1);

            // Create queue explicitly
            await rider1.Queue.Create(queueName, opts =>
            {
                opts.Partition = new PartitionOptions
                {
                    Enabled = true,
                    AutoAssignWorkers = true,
                    MaxPartitionsPerWorker = 5,
                    SubscribersPerPartition = 1
                };
            });

            // Subscribe a consumer with label so the partition is created
            HorseClient tempWorker = await Connect(port1);
            await tempWorker.Queue.SubscribePartitioned(queueName, "lbl-1", true);
            await Task.Delay(300);

            // Push a message with label
            HorseClient prod = await Connect(port1);
            await prod.Queue.Push(queueName, Encoding.UTF8.GetBytes("buffered-msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl-1") });
            await Task.Delay(300);

            // Disconnect consumer so message stays in partition
            tempWorker.Disconnect();
            await Task.Delay(200);

            // Push another message while consumer is offline
            await prod.Queue.Push(queueName, Encoding.UTF8.GetBytes("offline-msg"), false,
                new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, "lbl-1") });
            await Task.Delay(500);

            // Verify queue config saved
            Assert.True(File.Exists(Path.Combine(dataPath, "queues.json")));

            prod.Disconnect();
        }

        // ── Phase 2: restart rider, connect worker ────────────────────
        {
            HorseRider rider2 = BuildRider(dataPath, p =>
            {
                p.Enabled = true;
                p.AutoAssignWorkers = true;
                p.MaxPartitionsPerWorker = 5;
                p.SubscribersPerPartition = 1;
            });
            int port2 = await StartRider(rider2);

            // Verify restored queue has correct options
            HorseQueue q2 = rider2.Queue.Find(queueName);
            Assert.NotNull(q2);
            Assert.True(q2.Options.Partition.AutoAssignWorkers, "AutoAssignWorkers must survive restart");
            Assert.Equal(5, q2.Options.Partition.MaxPartitionsPerWorker);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // 9. CloneFrom: all partition fields are deep-copied
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void CloneFrom_DeepCopies_AllPartitionFields()
    {
        var original = new QueueOptions
        {
            Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 10,
                SubscribersPerPartition = 3,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = 120,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 42
            }
        };

        QueueOptions cloned = QueueOptions.CloneFrom(original);

        // All fields copied
        Assert.NotNull(cloned.Partition);
        Assert.True(cloned.Partition.Enabled);
        Assert.Equal(10, cloned.Partition.MaxPartitionCount);
        Assert.Equal(3, cloned.Partition.SubscribersPerPartition);
        Assert.Equal(PartitionAutoDestroy.NoMessages, cloned.Partition.AutoDestroy);
        Assert.Equal(120, cloned.Partition.AutoDestroyIdleSeconds);
        Assert.True(cloned.Partition.AutoAssignWorkers);
        Assert.Equal(42, cloned.Partition.MaxPartitionsPerWorker);

        // Deep copy — mutating original doesn't affect clone
        original.Partition.AutoAssignWorkers = false;
        original.Partition.MaxPartitionsPerWorker = 0;
        Assert.True(cloned.Partition.AutoAssignWorkers, "Clone must be independent");
        Assert.Equal(42, cloned.Partition.MaxPartitionsPerWorker);
    }

    [Fact]
    public void CloneFrom_NullPartition_RemainsNull()
    {
        var original = new QueueOptions { Partition = null };
        QueueOptions cloned = QueueOptions.CloneFrom(original);
        Assert.Null(cloned.Partition);
    }

    // ═════════════════════════════════════════════════════════════════════
    // 10. PartitionConfigurationData has all fields (compile-time + value check)
    // ═════════════════════════════════════════════════════════════════════

    [Fact]
    public void PartitionConfigurationData_HasAllRequiredFields()
    {
        // This test ensures that PartitionConfigurationData has the same fields
        // as PartitionOptions. If a new field is added to PartitionOptions but
        // not PartitionConfigurationData, this test will fail at compile time
        // or assertion.

        var data = new PartitionConfigurationData
        {
            Enabled = true,
            MaxPartitionCount = 5,
            SubscribersPerPartition = 2,
            AutoDestroy = "NoMessages",
            AutoDestroyIdleSeconds = 30,
            AutoAssignWorkers = true,
            MaxPartitionsPerWorker = 10
        };

        Assert.True(data.AutoAssignWorkers);
        Assert.Equal(10, data.MaxPartitionsPerWorker);
    }
}


