using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

public class QueueBindingTest
{
    [Fact]
    public async Task DistributeToMultipleQueues_AllConsumersReceive()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "dist-router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding { Name = "qbind-1", Target = "queue-1", Priority = 0, Interaction = BindingInteraction.Response });
            router.AddBinding(new QueueBinding { Name = "qbind-2", Target = "queue-2", Priority = 0, Interaction = BindingInteraction.Response });
            router.AddBinding(new QueueBinding { Name = "qbind-3", Target = "queue-3", Priority = 0, Interaction = BindingInteraction.Response });
            router.AddBinding(new QueueBinding { Name = "qbind-4", Target = "queue-4", Priority = 0, Interaction = BindingInteraction.Response });
            server.Rider.Router.Add(router);

            int received1 = 0;
            int received2 = 0;
            int received3 = 0;
            int received4 = 0;

            HorseClient consumer1 = new HorseClient();
            await consumer1.ConnectAsync("horse://localhost:" + port);
            await consumer1.Queue.Subscribe("queue-1", true, CancellationToken.None);
            consumer1.MessageReceived += (_, _) => Interlocked.Increment(ref received1);

            HorseClient consumer2 = new HorseClient();
            await consumer2.ConnectAsync("horse://localhost:" + port);
            await consumer2.Queue.Subscribe("queue-2", true, CancellationToken.None);
            consumer2.MessageReceived += (_, _) => Interlocked.Increment(ref received2);

            HorseClient consumer3 = new HorseClient();
            await consumer3.ConnectAsync("horse://localhost:" + port);
            await consumer3.Queue.Subscribe("queue-3", true, CancellationToken.None);
            consumer3.MessageReceived += (_, _) => Interlocked.Increment(ref received3);

            HorseClient consumer4 = new HorseClient();
            await consumer4.ConnectAsync("horse://localhost:" + port);
            await consumer4.Queue.Subscribe("queue-4", true, CancellationToken.None);
            consumer4.MessageReceived += (_, _) => Interlocked.Increment(ref received4);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            HorseResult result = await producer.Router.Publish("dist-router", Encoding.UTF8.GetBytes("Hello, World!"), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            await Task.Delay(1000);

            Assert.Equal(1, received1);
            Assert.Equal(1, received2);
            Assert.Equal(1, received3);
            Assert.Equal(1, received4);
        });
    }

    [Fact]
    public async Task DistributeBurst_AllConsumersReceiveFromCorrectQueue()
    {
        const int messageCount = 50;

        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("burst-q-1", o => { o.Type = QueueType.RoundRobin; o.Acknowledge = QueueAckDecision.JustRequest; });
            await server.Rider.Queue.Create("burst-q-2", o => { o.Type = QueueType.RoundRobin; o.Acknowledge = QueueAckDecision.JustRequest; });
            await server.Rider.Queue.Create("burst-q-3", o => { o.Type = QueueType.RoundRobin; o.Acknowledge = QueueAckDecision.JustRequest; });
            await server.Rider.Queue.Create("burst-q-4", o => { o.Type = QueueType.RoundRobin; o.Acknowledge = QueueAckDecision.JustRequest; });

            Router router = new Router(server.Rider, "burst-router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding { Name = "qbind-1", Target = "burst-q-1", Priority = 0, Interaction = BindingInteraction.Response });
            router.AddBinding(new QueueBinding { Name = "qbind-2", Target = "burst-q-2", Priority = 0, Interaction = BindingInteraction.Response });
            router.AddBinding(new QueueBinding { Name = "qbind-3", Target = "burst-q-3", Priority = 0, Interaction = BindingInteraction.Response });
            router.AddBinding(new QueueBinding { Name = "qbind-4", Target = "burst-q-4", Priority = 0, Interaction = BindingInteraction.Response });
            server.Rider.Router.Add(router);

            ConcurrentBag<string> sources1 = new();
            ConcurrentBag<string> sources2 = new();
            ConcurrentBag<string> sources3 = new();
            ConcurrentBag<string> sources4 = new();

            HorseClient consumer1 = new HorseClient();
            await consumer1.ConnectAsync("horse://localhost:" + port);
            await consumer1.Queue.Subscribe("burst-q-1", true, CancellationToken.None);
            consumer1.MessageReceived += (_, m) => sources1.Add(m.Target);

            HorseClient consumer2 = new HorseClient();
            await consumer2.ConnectAsync("horse://localhost:" + port);
            await consumer2.Queue.Subscribe("burst-q-2", true, CancellationToken.None);
            consumer2.MessageReceived += (_, m) => sources2.Add(m.Target);

            HorseClient consumer3 = new HorseClient();
            await consumer3.ConnectAsync("horse://localhost:" + port);
            await consumer3.Queue.Subscribe("burst-q-3", true, CancellationToken.None);
            consumer3.MessageReceived += (_, m) => sources3.Add(m.Target);

            HorseClient consumer4 = new HorseClient();
            await consumer4.ConnectAsync("horse://localhost:" + port);
            await consumer4.Queue.Subscribe("burst-q-4", true, CancellationToken.None);
            consumer4.MessageReceived += (_, m) => sources4.Add(m.Target);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            for (int i = 0; i < messageCount; i++)
            {
                HorseResult result = await producer.Router.Publish("burst-router", Encoding.UTF8.GetBytes($"msg-{i}"), true, CancellationToken.None);
                Assert.Equal(HorseResultCode.Ok, result.Code);
            }

            await Task.Delay(2000);

            Assert.Equal(messageCount, sources1.Count);
            Assert.Equal(messageCount, sources2.Count);
            Assert.Equal(messageCount, sources3.Count);
            Assert.Equal(messageCount, sources4.Count);

            Assert.All(sources1, t => Assert.Equal("burst-q-1", t));
            Assert.All(sources2, t => Assert.Equal("burst-q-2", t));
            Assert.All(sources3, t => Assert.Equal("burst-q-3", t));
            Assert.All(sources4, t => Assert.Equal("burst-q-4", t));
        });
    }

    [Fact]
    public async Task DistributeRandomBurst_EachConsumerReceivesOnlyFromOwnQueue()
    {
        const int messageCount = 100;

        await TestHorseRider.RunWith(async (server, port) =>
        {
            string[] queueNames = ["rand-q-1", "rand-q-2", "rand-q-3", "rand-q-4"];

            foreach (string q in queueNames)
                await server.Rider.Queue.Create(q, o => { o.Type = QueueType.RoundRobin; o.Acknowledge = QueueAckDecision.JustRequest; });

            ConcurrentBag<string>[] received = [new(), new(), new(), new()];

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                HorseClient consumer = new HorseClient();
                await consumer.ConnectAsync("horse://localhost:" + port);
                await consumer.Queue.Subscribe(queueNames[idx], true, CancellationToken.None);
                consumer.MessageReceived += (_, m) => received[idx].Add(m.Target);
            }

            int[] expectedCounts = new int[4];
            Random rnd = new Random(42);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            for (int i = 0; i < messageCount; i++)
            {
                int target = rnd.Next(0, 4);
                expectedCounts[target]++;

                HorseResult result = await producer.Queue.Push(queueNames[target], new System.IO.MemoryStream(Encoding.UTF8.GetBytes($"msg-{i}")), true, CancellationToken.None);
                Assert.Equal(HorseResultCode.Ok, result.Code);
            }

            await Task.Delay(2000);

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(expectedCounts[i], received[i].Count);
                Assert.All(received[i], t => Assert.Equal(queueNames[i], t));
            }
        });
    }
}
