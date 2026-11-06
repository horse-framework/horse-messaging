using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Events;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Common;
using Test.Events.Handlers.Queue;
using Xunit;

namespace Test.Events;

public class QueueEventTest
{
    [Fact]
    public async Task QueueCreate()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueCreateHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Queue.Create("test-queue", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueCreateHandler.Count);
        });
    }

    [Fact]
    public async Task QueueRemove()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueRemoveHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult createResult = await client.Queue.Create("test-queue", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, createResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, QueueRemoveHandler.Count);

            HorseResult removeResult = await client.Queue.Remove("test-queue", CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, removeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueRemoveHandler.Count);
        });
    }

    [Fact]
    public async Task QueueSubscribe()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueSubscribeHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Queue.Subscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueSubscribeHandler.Count);
        });
    }

    [Fact]
    public async Task QueueUnsubscribe()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueUnsubscribeHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult subscribeResult = await client.Queue.Subscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(0, QueueUnsubscribeHandler.Count);

            HorseResult unsubscribeResult = await client.Queue.Unsubscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, unsubscribeResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueUnsubscribeHandler.Count);
        });
    }
    [Fact]
    public async Task QueuePush()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueuePushHandler>();

            // Subscribing to the queue event auto-creates "test-queue".
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(250);

            // The push event is triggered without an explicit target; the server must fall back
            // to the queue name so the target-specific handler receives it.
            HorseResult pushResult = await client.Queue.Push("test-queue", new byte[] {1, 2, 3}, true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, pushResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueuePushHandler.Count);
        });
    }

    [Fact]
    public async Task QueueMessageAck()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            await server.Rider.Queue.Create("test-queue", o =>
            {
                o.Type = QueueType.Push;
                o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            });

            HorseClient client = new HorseClient();
            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueAckHandler>();
            await client.ConnectAsync($"horse://localhost:{port}");

            HorseClient consumer = new HorseClient { AutoAcknowledge = true };
            await consumer.ConnectAsync($"horse://localhost:{port}");
            HorseResult subscribeResult = await consumer.Queue.Subscribe("test-queue", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, subscribeResult.Code);
            await Task.Delay(250);

            HorseResult pushResult = await client.Queue.Push("test-queue", new byte[] {1, 2, 3}, true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, pushResult.Code);

            await Task.Delay(250);
            Assert.Equal(1, QueueAckHandler.Count);

            consumer.Disconnect();
            client.Disconnect();
        });
    }

    [Fact]
    public async Task QueueRemove_KeepsQueueEventSubscriptionForRecreatedQueue()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();
            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueuePushHandler>();
            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(250);

            HorseQueue queue = server.Rider.Queue.Find("test-queue");
            Assert.NotNull(queue);
            Assert.Single(queue.PushEvent.Subscribers);

            await server.Rider.Queue.Remove(queue);

            // Subscriptions are kept by queue name, a queue created later with the same name gets them.
            HorseQueue recreated = await server.Rider.Queue.Create("test-queue");
            Assert.NotSame(queue, recreated);
            Assert.Single(recreated.PushEvent.Subscribers);
        });
    }

    [Fact]
    public async Task QueueCreate_AllMatchingHandlersReceiveEvent()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();

            EventSubscriberRegistrar registrar = new EventSubscriberRegistrar(client.Event);
            registrar.RegisterHandler<QueueCreateTargetedHandler>();
            registrar.RegisterHandler<QueueCreateAllTargetsHandler>();

            await client.ConnectAsync($"horse://localhost:{port}");
            await Task.Delay(250);

            Assert.Equal(HorseResultCode.Ok, (await client.Queue.Create("multi-queue", CancellationToken.None)).Code);
            await Task.Delay(250);

            // A handler without a target must not shadow the handler registered for the target.
            Assert.Equal(1, QueueCreateTargetedHandler.Count);
            Assert.Equal(1, QueueCreateAllTargetsHandler.Count);

            Assert.Equal(HorseResultCode.Ok, (await client.Queue.Create("other-queue", CancellationToken.None)).Code);
            await Task.Delay(250);

            Assert.Equal(1, QueueCreateTargetedHandler.Count);
            Assert.Equal(2, QueueCreateAllTargetsHandler.Count);
        });
    }

    [Fact]
    public async Task UnsubscribeFromMissingQueue_DoesNotCreateQueue()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Event.Unsubscribe(HorseEventType.QueuePush, "missing-queue", true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.Null(server.Rider.Queue.Find("missing-queue"));
        });
    }

    [Fact]
    public async Task UnsubscribeFromMissingChannel_DoesNotCreateChannel()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Event.Unsubscribe(HorseEventType.ChannelPublish, "missing-channel", true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);
            Assert.Null(server.Rider.Channel.Find("missing-channel"));
        });
    }
}
