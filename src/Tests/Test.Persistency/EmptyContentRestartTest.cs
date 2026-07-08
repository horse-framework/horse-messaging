using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using Xunit;

namespace Test.Persistency;

public class EmptyContentRestartTest
{
    [Fact]
    public async Task Restart_LoadsEmptyContentMessageIntoPersistentQueue()
    {
        string dataPath = $"persist-empty-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";
        string queueName = $"persist-empty-q-{Guid.NewGuid():N}";

        HorseRider rider1 = null;
        HorseRider rider2 = null;
        HorseServer server1 = null;
        HorseServer server2 = null;

        try
        {
            rider1 = BuildRider(dataPath);
            server1 = await StartServer(rider1);

            HorseQueue queue1 = await rider1.Queue.Create(queueName, o =>
            {
                o.Type = QueueType.Push;
                o.CommitWhen = CommitWhen.AfterReceived;
                o.Acknowledge = QueueAckDecision.None;
            });

            HorseMessage message = new HorseMessage(MessageType.QueueMessage, queueName);
            message.SetMessageId("empty-1");
            message.Content = new MemoryStream(Array.Empty<byte>());
            message.CalculateLengths();

            PushResult pushResult = await queue1.Push(message);
            Assert.Equal(PushResult.Success, pushResult);
            Assert.Equal(1, queue1.Manager.MessageStore.Count());

            await server1.StopAsync();
            server1 = null;

            rider2 = BuildRider(dataPath);
            server2 = await StartServer(rider2);

            HorseQueue queue2 = rider2.Queue.Find(queueName);
            Assert.NotNull(queue2);
            Assert.Equal(1, queue2.Manager.MessageStore.Count());

            QueueMessage loaded = queue2.Manager.MessageStore.ReadFirst();
            Assert.NotNull(loaded);
            Assert.Equal("empty-1", loaded.Message.MessageId);
            Assert.Equal(0UL, loaded.Message.Length);
        }
        finally
        {
            try
            {
                if (server2 != null)
                    await server2.StopAsync();
            }
            catch
            {
            }

            try
            {
                if (server1 != null)
                    await server1.StopAsync();
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

    private static HorseRider BuildRider(string dataPath)
    {
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = dataPath)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.AfterReceived;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.Options.AutoQueueCreation = true;

                q.UsePersistentQueues(
                    db => db.UseInstantFlush().SetAutoShrink(false),
                    queue =>
                    {
                        queue.Options.CommitWhen = CommitWhen.AfterReceived;
                        queue.Options.Acknowledge = QueueAckDecision.None;
                    });
            })
            .Build();
    }

    private static async Task<HorseServer> StartServer(HorseRider rider)
    {
        for (int i = 0; i < 50; i++)
        {
            try
            {
                int port = Random.Shared.Next(12000, 62000);
                HorseServerOptions options = HorseServerOptions.CreateDefault();
                options.Hosts[0].Port = port;
                options.PingInterval = 300;
                options.RequestTimeout = 300;

                HorseServer server = new HorseServer(options);
                server.UseRider(rider);
                await server.StartAsync();
                await Task.Delay(150);
                return server;
            }
            catch
            {
                await Task.Delay(10);
            }
        }

        throw new InvalidOperationException("Could not start test server");
    }
}
