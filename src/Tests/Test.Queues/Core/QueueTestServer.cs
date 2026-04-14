using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Protocol;
using Horse.Server;

namespace Test.Queues.Core;

/// <summary>
/// Shared factory for queue tests.
/// Creates either Memory or Persistent backed servers via mode string.
/// Every test should use QueueTestServer.Create inside a try/finally and
/// call ctx.Server.StopAsync() in the finally block before dispose cleanup runs.
/// </summary>
internal static class QueueTestServer
{
    public static Task<QueueTestContext> Create(string mode, Action<QueueOptions> configureOptions = null)
    {
        return mode switch
        {
            "memory" => CreateMemory(configureOptions),
            "persistent" => CreatePersistent(configureOptions),
            _ => throw new ArgumentException($"Unknown mode: {mode}")
        };
    }

    private static async Task<QueueTestContext> CreateMemory(Action<QueueOptions> configureOptions)
    {
        string dataPath = $"qt-mem-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
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
        return new QueueTestContext(rider, port, dataPath, server);
    }

    private static async Task<QueueTestContext> CreatePersistent(Action<QueueOptions> configureOptions)
    {
        string dataPath = $"qt-persist-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
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
        return new QueueTestContext(rider, port, dataPath, server);
    }

    private static async Task<(int port, HorseServer server)> StartServer(HorseRider rider)
    {
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
        return (port, horseServer);
    }
}

/// <summary>
/// Disposable context for queue tests. Cleans up data directory on dispose.
/// </summary>
internal class QueueTestContext : IAsyncDisposable
{
    public HorseRider Rider { get; }
    public int Port { get; }
    public string DataPath { get; }
    public HorseServer Server { get; }

    public QueueTestContext(HorseRider rider, int port, string dataPath, HorseServer server = null)
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
            if (Server is { IsRunning: true })
                await Server.StopAsync();
        }
        catch { /* best effort */ }

        try
        {
            if (Directory.Exists(DataPath))
                Directory.Delete(DataPath, true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
