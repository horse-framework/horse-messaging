using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

public class QueueShutdownTest
{
    [Fact]
    public async Task Shutdown_BlocksNewQueuePushes()
    {
        await using var ctx = await QueueTestServer.Create("memory", o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseQueue queue = await ctx.Rider.Queue.Create("shutdown-blocked", o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.BeginShutdownAsync(TimeSpan.Zero);

        HorseMessage message = new HorseMessage(MessageType.QueueMessage, queue.Name);
        message.SetStringContent("payload");

        PushResult result = await queue.Push(message);
        Assert.Equal(PushResult.StatusNotSupported, result);
    }

    [Fact]
    public async Task Shutdown_FlushesPersistentQueues()
    {
        string dataPath = $"shutdown-flush-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
        HorseServer server = null;

        try
        {
            HorseRider rider = HorseRiderBuilder.Create()
                .ConfigureOptions(o => o.DataPath = dataPath)
                .ConfigureQueues(q =>
                {
                    q.Options.Type = QueueType.Push;
                    q.Options.CommitWhen = CommitWhen.AfterReceived;
                    q.Options.Acknowledge = QueueAckDecision.None;
                    q.Options.AutoQueueCreation = true;

                    q.UsePersistentQueues(
                        db => db.UseAutoFlush(TimeSpan.FromHours(1)).SetAutoShrink(false),
                        queue =>
                        {
                            queue.Options.CommitWhen = CommitWhen.AfterReceived;
                            queue.Options.Acknowledge = QueueAckDecision.None;
                        });
                })
                .Build();

            (server, int port) = await StartServer(rider);

            const string queueName = "shutdown-flush-q";
            HorseQueue queue = await rider.Queue.Create(queueName, o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{port}");
            HorseResult pushResult = await producer.Queue.Push(queueName, new MemoryStream("persist-me"u8.ToArray()), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, pushResult.Code);
            string filename = Path.Combine(dataPath, $"{queueName}.hdb");
            Assert.Equal(0, new FileInfo(filename).Length);

            await rider.Queue.BeginShutdownAsync(TimeSpan.Zero);

            int afterShutdownCount = await LoadMessageCount(filename);
            Assert.Equal(1, afterShutdownCount);
        }
        finally
        {
            try
            {
                if (server != null)
                    await server.StopAsync();
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(dataPath))
                    Directory.Delete(dataPath, true);
            }
            catch
            {
            }
        }
    }

    private static async Task<int> LoadMessageCount(string filename)
    {
        Database database = new Database(new DatabaseOptions
        {
            Filename = filename,
            AutoFlush = false,
            AutoShrink = false,
            InstantFlush = true
        });

        try
        {
            return (await database.Open()).Count;
        }
        finally
        {
            await database.Close();
        }
    }

    private static async Task<(HorseServer server, int port)> StartServer(HorseRider rider)
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                int port = Random.Shared.Next(12000, 62000);
                var opts = HorseServerOptions.CreateDefault();
                opts.Hosts[0].Port = port;
                opts.PingInterval = 300;
                opts.RequestTimeout = 300;

                var server = new HorseServer(opts);
                server.UseRider(rider);
                await server.StartAsync();
                await Task.Delay(100);
                return (server, port);
            }
            catch
            {
                await Task.Delay(10);
            }
        }

        throw new InvalidOperationException("Could not start test server");
    }
}
