using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;

namespace Test.Cache;

/// <summary>
/// Shared factory for cache tests.
/// Every test should use: await using var ctx = await CacheTestServer.Create();
/// </summary>
internal static class CacheTestServer
{
    public static async Task<CacheTestContext> Create(Action<HorseCacheOptions> configureOptions = null)
    {
        return await Create(configureOptions, null);
    }

    public static async Task<CacheTestContext> Create(Action<HorseCacheOptions> configureOptions, Action<HorseCacheConfigurator> configureCache)
    {
        string dataPath = $"ct-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureCache(c =>
            {
                configureOptions?.Invoke(c.Options);
                configureCache?.Invoke(c);
            })
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;
                q.UseMemoryQueues();
            })
            .Build();

        int port = await StartServer(rider);
        return new CacheTestContext(rider, port, dataPath);
    }

    private static async Task<int> StartServer(HorseRider rider)
    {
        int port = 0;

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
                break;
            }
            catch
            {
                Thread.Sleep(5);
                port = 0;
            }
        }

        await Task.Delay(100);
        return port;
    }
}

internal class CacheTestContext : IAsyncDisposable
{
    public HorseRider Rider { get; }
    public int Port { get; }
    public string DataPath { get; }

    public CacheTestContext(HorseRider rider, int port, string dataPath)
    {
        Rider = rider;
        Port = port;
        DataPath = dataPath;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Rider.Server.StopAsync();
            string path = Rider.Options.DataPath;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // best effort cleanup
        }

        return ValueTask.CompletedTask;
    }
}

