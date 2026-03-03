using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Test.Queues.Partitions;

/// <summary>
/// Disposable context for partition tests. Cleans up data directory on dispose.
/// </summary>
internal class PartitionTestContext : IAsyncDisposable
{
    public HorseRider Rider { get; }
    public int Port { get; }
    public string DataPath { get; }
    public HorseServer Server { get; }

    public PartitionTestContext(HorseRider rider, int port, string dataPath, HorseServer server)
    {
        Rider = rider;
        Port = port;
        DataPath = dataPath;
        Server = server;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Server != null)
                await Server.StopAsync();
        }
        catch { /* best effort */ }

        try
        {
            if (Directory.Exists(DataPath))
                Directory.Delete(DataPath, true);
        }
        catch { /* best effort cleanup */ }
    }
}

/// <summary>
/// A minimal HorseRider setup for partition tests.
/// Supports both memory and persistent modes via the Create(mode) overload.
/// </summary>
internal static class PartitionTestServer
{
    /// <summary>
    /// Creates a partition test context for the given mode ("memory" or "persistent").
    /// Returns a disposable context with Rider, Port, DataPath.
    /// </summary>
    public static Task<PartitionTestContext> Create(string mode, Action<QueueOptions> configureOptions = null)
    {
        return mode switch
        {
            "memory" => CreateMemoryContext(configureOptions),
            "persistent" => CreatePersistentContext(configureOptions),
            _ => throw new ArgumentException($"Unknown mode: {mode}")
        };
    }

    private static async Task<PartitionTestContext> CreateMemoryContext(Action<QueueOptions> configureOptions)
    {
        string dataPath = $"pt-mem-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                configureOptions?.Invoke(q.Options);

                q.UseMemoryQueues(queue =>
                {
                    queue.Options.CommitWhen = q.Options.CommitWhen;
                    queue.Options.Acknowledge = q.Options.Acknowledge;
                });
            })
            .Build();

        var (port, server) = await StartServer(rider);
        return new PartitionTestContext(rider, port, dataPath, server);
    }

    private static async Task<PartitionTestContext> CreatePersistentContext(Action<QueueOptions> configureOptions)
    {
        string dataPath = $"pt-persist-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                configureOptions?.Invoke(q.Options);

                q.UsePersistentQueues(
                    db => db.UseInstantFlush().SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = q.Options.CommitWhen;
                        queue.Options.Acknowledge = q.Options.Acknowledge;
                    });
            })
            .Build();

        var (port, server) = await StartServer(rider);
        return new PartitionTestContext(rider, port, dataPath, server);
    }

    // ── Legacy methods (used by existing [Fact] tests) ──────────────────

    /// <summary>
    /// Creates a memory-backed server and returns a disposable PartitionTestContext.
    /// Prefer this over the tuple-returning Create() overload.
    /// </summary>
    public static async Task<PartitionTestContext> CreateContext()
    {
        var (rider, port, server) = await Create();
        string dataPath = $"pt-ctx-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
        return new PartitionTestContext(rider, port, dataPath, server);
    }

    public static async Task<(HorseRider rider, int port, HorseServer server)> Create()
    {
        string dataPath = $"pt-data-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                q.UseMemoryQueues(queue =>
                {
                    queue.Options.CommitWhen = CommitWhen.None;
                    queue.Options.Acknowledge = QueueAckDecision.None;
                });
            })
            .Build();

        var (port, server) = await StartServer(rider);
        return (rider, port, server);
    }

    /// <summary>
    /// Creates a HorseRider backed by PersistentQueues (file-based storage).
    /// Each test run uses a unique data directory that is cleaned up after the server is stopped.
    /// </summary>
    public static async Task<(HorseRider rider, int port, HorseServer server, string dataPath)> CreatePersistent()
    {
        string dataPath = $"pt-persist-{Environment.TickCount}-{new Random().Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                q.UsePersistentQueues(
                    db => db.UseInstantFlush().SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = CommitWhen.None;
                        queue.Options.Acknowledge = QueueAckDecision.None;
                    });
            })
            .Build();

        var (port, server) = await StartServer(rider);
        return (rider, port, server, dataPath);
    }

    private static async Task<(int port, HorseServer server)> StartServer(HorseRider rider)
    {
        int port = 0;
        Random portRnd = new Random();

        for (int i = 0; i < 50; i++)
        {
            try
            {
                port = portRnd.Next(10000, 60000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;

                var horseServer = new HorseServer(opts);
                horseServer.UseRider(rider);
                horseServer.StartAsync().GetAwaiter().GetResult();
                await Task.Delay(100);
                return (port, horseServer);
            }
            catch
            {
                Thread.Sleep(5);
                port = 0;
            }
        }

        await Task.Delay(100);
        return (port, null);
    }
}
