using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Test.Common;
using Xunit;

namespace Test.Routers;

/// <summary>
/// Bindings that take the queue name from the QUEUE_NAME header of the message
/// </summary>
public class HeaderQueueBindingTest
{
    private static HorseMessage CreateMessage(string routerName, string queueName)
    {
        HorseMessage message = new HorseMessage(MessageType.Router, routerName, 0);
        message.SetMessageId(Guid.NewGuid().ToString());
        message.AddHeader(HorseHeaders.QUEUE_NAME, queueName);
        message.SetStringContent("hello");
        return message;
    }

    /// <summary>
    /// Consumers find their registration by the target of the message, so it must be the queue name
    /// </summary>
    [Fact]
    public async Task DynamicQueueBinding_MessageTargetIsQueueName()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "dynamic-router", RouteMethod.Distribute);
            router.AddBinding(new DynamicQueueBinding { Name = "dynamic", Target = "dynamic-binding", Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            foreach (string queueName in new[] { "dynamic-q-1", "dynamic-q-2", "dynamic-q-1" })
                Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("dynamic-router", queueName)));

            HorseQueue q1 = server.Rider.Queue.Find("dynamic-q-1");
            HorseQueue q2 = server.Rider.Queue.Find("dynamic-q-2");
            Assert.NotNull(q1);
            Assert.NotNull(q2);
            Assert.Equal(2, q1.Manager.MessageStore.Count());
            Assert.Equal(1, q2.Manager.MessageStore.Count());
            Assert.Equal("dynamic-q-1", q1.Manager.MessageStore.ReadFirst().Message.Target);
            Assert.Equal("dynamic-q-2", q2.Manager.MessageStore.ReadFirst().Message.Target);
            Assert.NotSame(server.Rider.Queue.Options, q1.Options);
            Assert.NotSame(q1.Options, q2.Options);
        });
    }

    [Fact]
    public async Task AutoQueueBinding_MessageTargetIsQueueName()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "auto-router", RouteMethod.Distribute);
            router.AddBinding(new AutoQueueBinding { Name = "auto", Target = "auto-binding", Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("auto-router", "auto-q-1")));

            HorseQueue queue = server.Rider.Queue.Find("auto-q-1");
            Assert.NotNull(queue);
            Assert.Equal("auto-q-1", queue.Manager.MessageStore.ReadFirst().Message.Target);
            Assert.NotSame(server.Rider.Queue.Options, queue.Options);
        });
    }

    /// <summary>
    /// The binding keeps its queue once created. A removed queue is resolved again from the message.
    /// </summary>
    [Fact]
    public async Task AutoQueueBinding_ResolvesQueueAgainAfterRemoval()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            Router router = new Router(server.Rider, "auto-router", RouteMethod.Distribute);
            router.AddBinding(new AutoQueueBinding { Name = "auto", Target = "auto-binding", Interaction = BindingInteraction.None });
            server.Rider.Router.Add(router);

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("auto-router", "auto-q-r")));
            HorseQueue first = server.Rider.Queue.Find("auto-q-r");
            Assert.NotNull(first);

            await server.Rider.Queue.Remove(first);

            Assert.Equal(RouterPublishResult.OkWillNotRespond, await router.Publish(null, CreateMessage("auto-router", "auto-q-r")));
            HorseQueue second = server.Rider.Queue.Find("auto-q-r");
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.Equal(1, second.Manager.MessageStore.Count());
        });
    }
}
