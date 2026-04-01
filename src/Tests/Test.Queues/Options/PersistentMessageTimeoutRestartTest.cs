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
using Xunit;

namespace Test.Queues.Options;

public class PersistentMessageTimeoutRestartTest : IAsyncLifetime
{
    private readonly string _dataPath = $"mt-restart-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
    private HorseServer _server1;
    private HorseServer _server2;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try { if (_server2 != null) await _server2.StopAsync(); } catch { }
        try { if (_server1 != null) await _server1.StopAsync(); } catch { }

        try
        {
            if (Directory.Exists(_dataPath))
                Directory.Delete(_dataPath, true);
        }
        catch
        {
        }
    }

    private HorseRider BuildRider()
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = _dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                q.UsePersistentQueues(
                    db => db.SetPhysicalPath(queue => Path.Combine(_dataPath, $"{queue.Name}.hdb"))
                             .UseInstantFlush()
                             .SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = CommitWhen.AfterReceived;
                        queue.Options.Acknowledge = QueueAckDecision.None;
                    });
            })
            .Build();
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
                await Task.Delay(150);
                return (server, port);
            }
            catch
            {
                await Task.Delay(10);
            }
        }

        throw new InvalidOperationException("Could not start test server");
    }

    [Fact]
    public async Task Restart_LoadedPersistentMessages_GetFreshDeadlineFromQueueTimeout()
    {
        const string queueName = "mt-restart-deadline";

        HorseRider rider1 = BuildRider();
        (_server1, int port1) = await StartServer(rider1);

        await rider1.Queue.Create(queueName, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
            o.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = 10,
                Policy = MessageTimeoutPolicy.Delete
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{port1}");
        HorseResult push = await producer.Queue.Push(queueName, new MemoryStream("payload"u8.ToArray()), false, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, push.Code);

        HorseQueue queue1 = rider1.Queue.Find(queueName);
        Assert.NotNull(queue1);

        QueueMessage original = null;
        for (int i = 0; i < 20 && original == null; i++)
        {
            original = queue1.Manager.MessageStore.ReadFirst();
            if (original == null)
                await Task.Delay(100);
        }

        Assert.NotNull(original);
        Assert.True(original.Deadline.HasValue);

        HorseRider rider2 = BuildRider();
        (_server2, _) = await StartServer(rider2);

        HorseQueue queue2 = null;
        for (int i = 0; i < 20 && queue2 == null; i++)
        {
            queue2 = rider2.Queue.Find(queueName);
            if (queue2 == null)
                await Task.Delay(100);
        }

        Assert.NotNull(queue2);

        QueueMessage loaded = null;
        for (int i = 0; i < 20 && loaded == null; i++)
        {
            loaded = queue2.Manager.MessageStore.ReadFirst();
            if (loaded == null)
                await Task.Delay(100);
        }

        Assert.NotNull(loaded);
        Assert.True(loaded.Deadline.HasValue, "Diskten yüklenen mesaj deadline almalı");

        TimeSpan remaining = loaded.Deadline.Value - DateTime.UtcNow;
        Assert.InRange(remaining.TotalSeconds, 7, 12);

        producer.Disconnect();
    }
}
