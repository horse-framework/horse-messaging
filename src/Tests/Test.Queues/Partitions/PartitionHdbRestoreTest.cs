using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
using Xunit.Abstractions;

namespace Test.Queues.Partitions;

/// <summary>
/// Tests for the scenario where partition sub-queue entries leak into queues.json
/// (e.g. from an older version that did not set SkipPersistence) and are restored
/// on server restart.
///
/// Expected behaviour:
///   - Sub-queue entries with SubPartition data are cleaned from queues.json on startup.
///   - Sub-queue entries detected by name pattern (legacy) are also cleaned.
///   - After cleanup, only the parent queue is restored with IsPartitioned=true.
///   - Sub-queue entries that somehow survive cleanup must NOT become independent
///     parent queues that accept direct subscribe without partition routing.
///
/// Real-world scenario from Parrot production:
///   If sub-queues like "FetchRawOrdersEvent-Partition-free" are loaded as
///   independent (non-partitioned) queues, any client that subscribes to them
///   directly bypasses PartitionManager label routing and lands in the wrong tier.
/// </summary>
public class PartitionHdbRestoreTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<string> _dataPaths = new();
    private HorseServer _server;

    public PartitionHdbRestoreTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        try { _server?.StopAsync().GetAwaiter().GetResult(); } catch { }

        foreach (string path in _dataPaths)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* best-effort cleanup */ }
        }
    }

    #region Helpers

    private static string NewDataPath()
    {
        return $"pt-hdb-{Environment.TickCount}-{new Random().Next(0, 100000)}";
    }

    private HorseRider BuildRider(string dataPath)
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

                q.UseCustomPersistentConfigurator(new QueueOptionsConfigurator(q.Rider, "queues.json"));
            })
            .Build();
    }

    private async Task<int> StartServer(HorseRider rider)
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
                _server = new HorseServer(opts);
                _server.UseRider(rider);
                _server.StartAsync().GetAwaiter().GetResult();
                await Task.Delay(120);
                return port;
            }
            catch { Thread.Sleep(5); }
        }

        throw new InvalidOperationException("Could not bind any port after 50 attempts");
    }

    private static async Task<HorseClient> ConnectOnly(int port)
    {
        HorseClient client = new HorseClient();
        client.AutoSubscribe = false;
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    private static async Task SubscribeWithLabel(HorseClient client, string queue, string label)
    {
        HorseResult result = await client.Queue.Subscribe(queue, true,
            new[] { new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, label) },
            CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);
    }

    /// <summary>
    /// Creates a real server, creates the parent queue, stops the server,
    /// then injects sub-queue entries into queues.json to simulate the legacy bug.
    /// Returns the dataPath for reuse.
    /// </summary>
    private string CreateDataWithInjectedSubQueues(string parentQueueName, string[] labels)
    {
        string dataPath = NewDataPath();
        Directory.CreateDirectory(dataPath);

        // Phase 1: Boot a real server to produce a valid queues.json with the parent queue
        var rider1 = BuildRider(dataPath);
        var opts = HorseServerOptions.CreateDefault();
        opts.Hosts[0].Port = new Random().Next(20000, 60000);
        opts.PingInterval = 300;
        opts.RequestTimeout = 300;
        var server1 = new HorseServer(opts);
        server1.UseRider(rider1);
        server1.StartAsync().GetAwaiter().GetResult();

        rider1.Queue.Create(parentQueueName, o =>
        {
            o.Type = QueueType.Push;
            o.Acknowledge = QueueAckDecision.None;
            o.CommitWhen = CommitWhen.None;
            o.Partition = new PartitionOptions
            {
                Enabled = true,
                MaxPartitionCount = 0,
                SubscribersPerPartition = 1,
                AutoDestroy = PartitionAutoDestroy.NoMessages,
                AutoDestroyIdleSeconds = 300,
                AutoAssignWorkers = true,
                MaxPartitionsPerWorker = 1
            };
        }).GetAwaiter().GetResult();

        server1.StopAsync().GetAwaiter().GetResult();

        // Phase 2: Read the queues.json, clone the parent entry for sub-queues, inject them
        string queuesJsonPath = Path.Combine(dataPath, "queues.json");
        string json = File.ReadAllText(queuesJsonPath);
        var configs = JsonSerializer.Deserialize<List<QueueConfiguration>>(json, _jsonOptions);

        QueueConfiguration parentCfg = configs.First(c => c.Name == parentQueueName);

        foreach (string label in labels)
        {
            // Clone the parent config but make it look like a legacy sub-queue:
            // - Name = "{parent}-Partition-{label}"
            // - Partition = null (sub-queues don't have partition config)
            // - SubPartition = null (legacy bug: SubPartition was not set)
            string subJson = JsonSerializer.Serialize(parentCfg, _jsonOptions);
            var subCfg = JsonSerializer.Deserialize<QueueConfiguration>(subJson, _jsonOptions);
            subCfg.Name = $"{parentQueueName}-Partition-{label}";
            subCfg.Partition = null;
            subCfg.SubPartition = null;
            subCfg.ClientLimit = 1;
            configs.Add(subCfg);
        }

        string updatedJson = JsonSerializer.Serialize(configs, _jsonOptions);
        File.WriteAllText(queuesJsonPath, updatedJson);

        return dataPath;
    }


    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = null };

    #endregion

    /// <summary>
    /// Verifies that QueueRider.Initialize() correctly removes sub-queue entries
    /// from queues.json when they match the "{parent}-Partition-{label}" name pattern
    /// and have no Partition config (legacy detection).
    ///
    /// After startup only the parent queue should exist, NOT the sub-queues.
    /// </summary>
    [Fact]
    public async Task Restart_SubQueueEntries_CleanedFromQueuesJson()
    {
        string dataPath = CreateDataWithInjectedSubQueues("FetchRawOrdersEvent", new[] { "free", "standard", "premium" });
        _dataPaths.Add(dataPath);


        // Start the server — QueueRider.Initialize() should clean sub-queue entries
        HorseRider rider = BuildRider(dataPath);
        int port = await StartServer(rider);

        // Verify: only parent queue exists, sub-queues were cleaned
        HorseQueue parent = rider.Queue.Find("FetchRawOrdersEvent");
        Assert.NotNull(parent);
        Assert.True(parent.IsPartitioned, "Parent queue must be partitioned");
        Assert.NotNull(parent.PartitionManager);

        HorseQueue subFree = rider.Queue.Find("FetchRawOrdersEvent-Partition-free");
        HorseQueue subStandard = rider.Queue.Find("FetchRawOrdersEvent-Partition-standard");
        HorseQueue subPremium = rider.Queue.Find("FetchRawOrdersEvent-Partition-premium");

        _output.WriteLine($"Parent: {parent?.Name} IsPartitioned={parent?.IsPartitioned}");
        _output.WriteLine($"Sub-free: {subFree?.Name}");
        _output.WriteLine($"Sub-standard: {subStandard?.Name}");
        _output.WriteLine($"Sub-premium: {subPremium?.Name}");

        Assert.Null(subFree);
        Assert.Null(subStandard);
        Assert.Null(subPremium);

        // Verify the queues.json on disk no longer has sub-queue entries
        string json = File.ReadAllText(Path.Combine(dataPath, "queues.json"));
        Assert.DoesNotContain("Partition-free", json);
        Assert.DoesNotContain("Partition-standard", json);
        Assert.DoesNotContain("Partition-premium", json);
        Assert.Contains("FetchRawOrdersEvent", json);
    }

    /// <summary>
    /// Verifies that after server restart with a clean queues.json (sub-queues removed),
    /// the parent queue is correctly restored as partitioned and labeled subscribe works.
    /// Clients subscribing with labels must go through PartitionManager, not directly to
    /// a stale sub-queue.
    /// </summary>
    [Fact]
    public async Task Restart_ParentQueue_PreservesPartitionConfig_LabeledSubscribeWorks()
    {
        string dataPath = CreateDataWithInjectedSubQueues("CompareOrdersEvent", new[] { "free", "standard" });
        _dataPaths.Add(dataPath);


        // Start server
        HorseRider rider = BuildRider(dataPath);
        int port = await StartServer(rider);

        HorseQueue parent = rider.Queue.Find("CompareOrdersEvent");
        Assert.NotNull(parent);
        Assert.True(parent.IsPartitioned);

        // Client subscribes with label → PartitionManager creates sub-queue on demand
        HorseClient clientFree = await ConnectOnly(port);
        HorseClient clientStandard = await ConnectOnly(port);

        await SubscribeWithLabel(clientFree, "CompareOrdersEvent", "free");
        await SubscribeWithLabel(clientStandard, "CompareOrdersEvent", "standard");

        await Task.Delay(500);

        // Verify: 2 partitions created by PartitionManager
        Assert.Equal(2, parent.PartitionManager.Partitions.Count());

        var freeEntry = parent.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "free");
        var stdEntry = parent.PartitionManager.Partitions.FirstOrDefault(p => p.Label == "standard");
        Assert.NotNull(freeEntry);
        Assert.NotNull(stdEntry);

        // Verify: each client is ONLY in its own label's partition
        Assert.Contains(freeEntry.Queue.Clients, c => c.Client.UniqueId == clientFree.ClientId);
        Assert.DoesNotContain(stdEntry.Queue.Clients, c => c.Client.UniqueId == clientFree.ClientId);

        Assert.Contains(stdEntry.Queue.Clients, c => c.Client.UniqueId == clientStandard.ClientId);
        Assert.DoesNotContain(freeEntry.Queue.Clients, c => c.Client.UniqueId == clientStandard.ClientId);

        _output.WriteLine("Labeled subscribe after restart works correctly through PartitionManager");
    }

}

