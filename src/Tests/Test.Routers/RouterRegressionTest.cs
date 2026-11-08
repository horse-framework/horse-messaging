using System;
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

public class RouterRegressionTest
{
    [Fact]
    public async Task RouterLimit_AllowsRoutersUpToTheLimit()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            server.Rider.Options.RouterLimit = 2;

            server.Rider.Router.Add("limit-1", RouteMethod.Distribute);
            server.Rider.Router.Add(new Router(server.Rider, "limit-2", RouteMethod.Distribute));

            Assert.Throws<OperationCanceledException>(() => server.Rider.Router.Add("limit-3", RouteMethod.Distribute));
            Assert.Throws<OperationCanceledException>(() => server.Rider.Router.Add(new Router(server.Rider, "limit-4", RouteMethod.Distribute)));
            Assert.Null(server.Rider.Router.Find("limit-3"));
            Assert.Null(server.Rider.Router.Find("limit-4"));

            await Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(RouteMethod.Distribute)]
    [InlineData(RouteMethod.OnlyFirst)]
    [InlineData(RouteMethod.RoundRobin)]
    public async Task DirectBinding_ProducerReceivesAcknowledge(RouteMethod method)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "ack-router", method);
            router.AddBinding(new DirectBinding { Name = "direct", Target = "client-1", Interaction = BindingInteraction.None, RouteMethod = RouteMethod.Distribute });
            server.Rider.Router.Add(router);

            HorseClient client1 = new HorseClient { ClientId = "client-1" };
            await client1.ConnectAsync("horse://localhost:" + port);
            Assert.True(client1.IsConnected);

            HorseClient producer = new HorseClient { ResponseTimeout = TimeSpan.FromSeconds(3) };
            await producer.ConnectAsync("horse://localhost:" + port);
            Assert.True(producer.IsConnected);

            HorseResult result = await producer.Router.Publish("ack-router", Encoding.UTF8.GetBytes("hello"), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);
        });
    }

    /// <summary>
    /// The queue sets the acknowledge flag of a stored message by its own options.
    /// The router must not write the producer's flag back onto a message the queue already holds.
    /// </summary>
    [Theory]
    [InlineData(RouteMethod.Distribute)]
    [InlineData(RouteMethod.OnlyFirst)]
    [InlineData(RouteMethod.RoundRobin)]
    public async Task QueueBinding_StoredMessageKeepsQueueAcknowledgeFlag(RouteMethod method)
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseQueue queue = await server.Rider.Queue.Create("ack-request-queue", o =>
            {
                o.Type = QueueType.Push;
                o.Acknowledge = QueueAckDecision.JustRequest;
            });

            Router router = new Router(server.Rider, "ack-flag-router", method);
            router.AddBinding(new QueueBinding { Name = "queue", Target = "ack-request-queue", Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            HorseMessage message = new HorseMessage(MessageType.Router, "ack-flag-router", 0);
            message.SetMessageId(Guid.NewGuid().ToString());
            message.WaitResponse = false;
            message.SetStringContent("hello");

            RouterPublishResult result = await router.Publish(null, message);
            Assert.Equal(RouterPublishResult.OkWillNotRespond, result);

            QueueMessage stored = queue.Manager.MessageStore.ReadFirst();
            Assert.NotNull(stored);
            Assert.True(stored.Message.WaitResponse);
        });
    }

    [Fact]
    public async Task QueueBinding_AutoCreatedQueueDoesNotChangeServerDefaults()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "auto-create-router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding { Name = "queue", Target = "auto-created-queue", Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            QueueType defaultType = server.Rider.Queue.Options.Type;

            HorseMessage message = new HorseMessage(MessageType.Router, "auto-create-router", 0);
            message.SetMessageId(Guid.NewGuid().ToString());
            message.AddHeader(HorseHeaders.QUEUE_TYPE, "round-robin");
            message.SetStringContent("hello");
            await router.Publish(null, message);

            HorseQueue queue = server.Rider.Queue.Find("auto-created-queue");
            Assert.NotNull(queue);
            Assert.NotSame(server.Rider.Queue.Options, queue.Options);
            Assert.Equal(QueueType.RoundRobin, queue.Options.Type);
            Assert.Equal(defaultType, server.Rider.Queue.Options.Type);
        });
    }

    /// <summary>
    /// The binding caches its queue. A removed queue must not be used from the cache.
    /// </summary>
    [Fact]
    public async Task QueueBinding_UsesQueueCreatedAgainAfterRemoval()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseQueue first = await server.Rider.Queue.Create("recreated-queue", o => o.Type = QueueType.Push);

            Router router = new Router(server.Rider, "recreate-router", RouteMethod.Distribute);
            router.AddBinding(new QueueBinding { Name = "queue", Target = "recreated-queue", Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("recreate-router")));
            Assert.Equal(1, first.Manager.MessageStore.Count());

            await server.Rider.Queue.Remove(first);
            HorseQueue second = await server.Rider.Queue.Create("recreated-queue", o => o.Type = QueueType.Push);
            Assert.NotSame(first, second);

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("recreate-router")));
            Assert.Equal(1, second.Manager.MessageStore.Count());
        });
    }

    private static HorseMessage CreateMessage(string routerName)
    {
        HorseMessage message = new HorseMessage(MessageType.Router, routerName, 0);
        message.SetMessageId(Guid.NewGuid().ToString());
        message.SetStringContent("hello");
        return message;
    }
}
