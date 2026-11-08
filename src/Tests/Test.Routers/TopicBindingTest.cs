using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

public class TopicBindingTest
{
    private static async Task<(HorseQueue, HorseQueue)> CreateTopicQueues(TestHorseRider server, int firstQueueLimit = 0)
    {
        HorseQueue q1 = await server.Rider.Queue.Create("orders-a", o =>
        {
            o.Type = QueueType.Push;
            o.MessageLimit = firstQueueLimit;
        });

        HorseQueue q2 = await server.Rider.Queue.Create("orders-b", o => o.Type = QueueType.Push);
        q1.Topic = "orders";
        q2.Topic = "orders";
        return (q1, q2);
    }

    private static Router CreateRouter(TestHorseRider server, RouteMethod bindingMethod)
    {
        Router router = new Router(server.Rider, "topic-router", RouteMethod.Distribute);
        router.AddBinding(new TopicBinding { Name = "topic", Target = "orders", Interaction = BindingInteraction.None, RouteMethod = bindingMethod });
        server.Rider.Router.Add(router);
        return router;
    }

    private static HorseMessage CreateMessage()
    {
        HorseMessage message = new HorseMessage(MessageType.Router, "topic-router", 0);
        message.SetMessageId(Guid.NewGuid().ToString());
        message.SetStringContent("order");
        return message;
    }

    [Fact]
    public async Task Distribute_PushesToEveryTopicQueueThroughQueuePush()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            (HorseQueue q1, HorseQueue q2) = await CreateTopicQueues(server);
            Router router = CreateRouter(server, RouteMethod.Distribute);
            int receivedBefore = server.OnReceived;

            RouterPublishResult result = await router.Publish(null, CreateMessage());

            Assert.Equal(RouterPublishResult.OkWillNotRespond, result);
            Assert.Equal(1, q1.Manager.MessageStore.Count());
            Assert.Equal(1, q2.Manager.MessageStore.Count());

            // The delivery handler (where persistent queues save the message) is called by the queue's push pipeline
            Assert.Equal(2, server.OnReceived - receivedBefore);
        });
    }

    [Fact]
    public async Task Distribute_QueueRejectingMessageDoesNotStopOtherQueues()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            (HorseQueue q1, HorseQueue q2) = await CreateTopicQueues(server, firstQueueLimit: 1);
            Router router = CreateRouter(server, RouteMethod.Distribute);

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));
            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            Assert.Equal(1, q1.Manager.MessageStore.Count());
            Assert.Equal(2, q2.Manager.MessageStore.Count());
        });
    }

    [Fact]
    public async Task RoundRobin_AlternatesBetweenTopicQueues()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            (HorseQueue q1, HorseQueue q2) = await CreateTopicQueues(server);
            Router router = CreateRouter(server, RouteMethod.RoundRobin);

            for (int i = 0; i < 4; i++)
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            Assert.Equal(2, q1.Manager.MessageStore.Count());
            Assert.Equal(2, q2.Manager.MessageStore.Count());
            Assert.Null(server.Rider.Queue.Find("topic-router"));
        });
    }

    [Fact]
    public async Task OnlyFirst_PushesEveryMessageToOneTopicQueue()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            (HorseQueue q1, HorseQueue q2) = await CreateTopicQueues(server);
            Router router = CreateRouter(server, RouteMethod.OnlyFirst);

            for (int i = 0; i < 3; i++)
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            int c1 = q1.Manager.MessageStore.Count();
            int c2 = q2.Manager.MessageStore.Count();
            Assert.Equal(3, c1 + c2);
            Assert.Equal(3, Math.Max(c1, c2));
            Assert.Null(server.Rider.Queue.Find("topic-router"));
        });
    }

    [Fact]
    public async Task OnlyFirst_FallsBackToNextQueueWhenFirstRejects()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            (HorseQueue q1, HorseQueue q2) = await CreateTopicQueues(server, firstQueueLimit: 1);
            Router router = CreateRouter(server, RouteMethod.OnlyFirst);

            for (int i = 0; i < 3; i++)
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage()));

            Assert.True(q1.Manager.MessageStore.Count() <= 1);
            Assert.Equal(3, q1.Manager.MessageStore.Count() + q2.Manager.MessageStore.Count());
        });
    }
}
