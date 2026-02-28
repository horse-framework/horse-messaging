using System;
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
/// A minimal HorseRider setup for partition tests.
/// Uses MemoryQueues with CommitWhen.None so delivery semantics don't
/// interfere with partition routing assertions.
/// </summary>
internal static class PartitionTestServer
{
    public static async Task<(HorseRider rider, int port, HorseServer server)> Create()
    {
        HorseRider rider = HorseRiderBuilder.Create()
            .ConfigureOptions(o =>
            {
                Random rnd = new Random();
                o.DataPath = $"pt-data-{Environment.TickCount}-{rnd.Next(0, 100000)}";
            })
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

        return (rider, await StartServer(rider), null);
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

        int port = await StartServer(rider);
        return (rider, port, null, dataPath);
    }

    private static async Task<int> StartServer(HorseRider rider)
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
