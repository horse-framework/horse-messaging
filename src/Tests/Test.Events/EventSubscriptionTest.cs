using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Test.Common;
using Xunit;

namespace Test.Events;

public class EventSubscriptionTest
{
    private static HorseClient CreateEventClient(ConcurrentQueue<HorseMessage> received)
    {
        HorseClient client = new HorseClient { AutoSubscribe = false, CatchEventMessages = true };
        client.MessageReceived += (_, message) =>
        {
            if (message.Type == MessageType.Event)
                received.Enqueue(message);
        };
        return client;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < until)
            await Task.Delay(20);
    }

    [Fact]
    public async Task ClientDisconnect_RemovesEventSubscriptions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient { AutoSubscribe = false };
            await client.ConnectAsync($"horse://localhost:{port}");

            Assert.Equal(HorseResultCode.Ok, (await client.Event.Subscribe(HorseEventType.QueueCreate, null, true, CancellationToken.None)).Code);
            Assert.Equal(HorseResultCode.Ok, (await client.Event.Subscribe(HorseEventType.QueuePush, "disconnect-queue", true, CancellationToken.None)).Code);

            HorseQueue queue = server.Rider.Queue.Find("disconnect-queue");
            Assert.NotNull(queue);
            Assert.Single(server.Rider.Queue.CreateEvent.Subscribers);
            Assert.Single(queue.PushEvent.Subscribers);

            client.Disconnect();

            // removed on disconnect, not by a periodic cleanup
            await WaitUntil(() => server.Rider.Queue.CreateEvent.Subscribers.Count == 0 && queue.PushEvent.Subscribers.Count == 0);
            Assert.Empty(server.Rider.Queue.CreateEvent.Subscribers);
            Assert.Empty(queue.PushEvent.Subscribers);
        });
    }

    [Fact]
    public async Task QueuePushEvent_IsReceivedAfterQueueIsRemovedAndCreatedAgain()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ConcurrentQueue<HorseMessage> received = new();
            HorseClient subscriber = CreateEventClient(received);
            await subscriber.ConnectAsync($"horse://localhost:{port}");

            Assert.Equal(HorseResultCode.Ok, (await subscriber.Event.Subscribe(HorseEventType.QueuePush, "recreate-queue", true, CancellationToken.None)).Code);

            HorseQueue queue = server.Rider.Queue.Find("recreate-queue");
            Assert.NotNull(queue);
            await server.Rider.Queue.Remove(queue);
            Assert.Null(server.Rider.Queue.Find("recreate-queue"));

            HorseClient producer = new HorseClient { AutoSubscribe = false };
            await producer.ConnectAsync($"horse://localhost:{port}");
            Assert.Equal(HorseResultCode.Ok, (await producer.Queue.Push("recreate-queue", new byte[] {1}, true, CancellationToken.None)).Code);

            await WaitUntil(() => !received.IsEmpty);
            Assert.True(received.TryDequeue(out HorseMessage message));
            Assert.Equal((ushort) HorseEventType.QueuePush, message.ContentType);
            Assert.Equal("recreate-queue", message.Target);

            subscriber.Disconnect();
            producer.Disconnect();
        });
    }

    [Fact]
    public async Task UnsubscribeFromRemovedQueue_RemovesSubscription()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient { AutoSubscribe = false };
            await client.ConnectAsync($"horse://localhost:{port}");

            Assert.Equal(HorseResultCode.Ok, (await client.Event.Subscribe(HorseEventType.QueuePush, "orphan-queue", true, CancellationToken.None)).Code);
            await server.Rider.Queue.Remove(server.Rider.Queue.Find("orphan-queue"));

            Assert.Equal(HorseResultCode.Ok, (await client.Event.Unsubscribe(HorseEventType.QueuePush, "orphan-queue", true, CancellationToken.None)).Code);
            Assert.Null(server.Rider.Queue.Find("orphan-queue"));

            HorseQueue recreated = await server.Rider.Queue.Create("orphan-queue");
            Assert.Empty(recreated.PushEvent.Subscribers);

            client.Disconnect();
        });
    }

    [Fact]
    public async Task ChannelRemove_RemovesPublishEventSubscriptions()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ConcurrentQueue<HorseMessage> received = new();
            HorseClient subscriber = CreateEventClient(received);
            await subscriber.ConnectAsync($"horse://localhost:{port}");

            Assert.Equal(HorseResultCode.Ok, (await subscriber.Event.Subscribe(HorseEventType.ChannelPublish, "remove-channel", true, CancellationToken.None)).Code);

            HorseChannel channel = server.Rider.Channel.Find("remove-channel");
            Assert.NotNull(channel);
            Assert.Single(channel.PublishEvent.Subscribers);

            server.Rider.Channel.Remove(channel);
            Assert.Empty(channel.PublishEvent.Subscribers);

            // subscriptions end with the channel, a channel created later with the same name starts without them
            HorseChannel recreated = await server.Rider.Channel.Create("remove-channel");
            Assert.NotSame(channel, recreated);
            Assert.Empty(recreated.PublishEvent.Subscribers);

            Assert.Equal(PushResult.Success, recreated.Push("first"));
            await Task.Delay(500);
            Assert.Empty(received);

            // subscribing again subscribes to the new channel
            Assert.Equal(HorseResultCode.Ok, (await subscriber.Event.Subscribe(HorseEventType.ChannelPublish, "remove-channel", true, CancellationToken.None)).Code);
            Assert.Single(recreated.PublishEvent.Subscribers);

            Assert.Equal(PushResult.Success, recreated.Push("second"));
            await WaitUntil(() => !received.IsEmpty);
            Assert.True(received.TryDequeue(out HorseMessage message));
            Assert.Equal((ushort) HorseEventType.ChannelPublish, message.ContentType);
            Assert.Equal("remove-channel", message.Target);

            subscriber.Disconnect();
        });
    }

    [Fact]
    public async Task ChannelPublishEvent_Unsubscribe_RemovesSubscription()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient { AutoSubscribe = false };
            await client.ConnectAsync($"horse://localhost:{port}");

            Assert.Equal(HorseResultCode.Ok, (await client.Event.Subscribe(HorseEventType.ChannelPublish, "unsubscribe-channel", true, CancellationToken.None)).Code);
            HorseChannel channel = server.Rider.Channel.Find("unsubscribe-channel");
            Assert.NotNull(channel);
            Assert.Single(channel.PublishEvent.Subscribers);

            Assert.Equal(HorseResultCode.Ok, (await client.Event.Unsubscribe(HorseEventType.ChannelPublish, "unsubscribe-channel", true, CancellationToken.None)).Code);
            Assert.Empty(channel.PublishEvent.Subscribers);

            client.Disconnect();
        });
    }

    /// <summary>
    /// A user handler that throws synchronously (not inside a returned task)
    /// </summary>
    private class ThrowingQueueHandler : IQueueEventHandler
    {
        public Task OnCreated(HorseQueue queue) => Task.CompletedTask;
        public Task OnRemoved(HorseQueue queue) => Task.CompletedTask;
        public Task OnConsumerSubscribed(QueueClient client) => Task.CompletedTask;
        public Task OnConsumerUnsubscribed(QueueClient client) => throw new InvalidOperationException("handler failed");
        public Task OnStatusChanged(HorseQueue queue, QueueStatus from, QueueStatus to) => Task.CompletedTask;
    }

    [Fact]
    public async Task ClientDisconnect_RemovesEventSubscriptions_WhenQueueHandlerThrows()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            server.Rider.Queue.EventHandlers.Add(new ThrowingQueueHandler());

            HorseClient client = new HorseClient { AutoSubscribe = false };
            await client.ConnectAsync($"horse://localhost:{port}");

            Assert.Equal(HorseResultCode.Ok, (await client.Queue.Subscribe("throwing-handler-queue", true, CancellationToken.None)).Code);
            Assert.Equal(HorseResultCode.Ok, (await client.Event.Subscribe(HorseEventType.QueueCreate, null, true, CancellationToken.None)).Code);
            Assert.Single(server.Rider.Queue.CreateEvent.Subscribers);

            // removing the queue subscription on disconnect calls the throwing handler
            client.Disconnect();

            await WaitUntil(() => server.Rider.Queue.CreateEvent.Subscribers.Count == 0);
            Assert.Empty(server.Rider.Queue.CreateEvent.Subscribers);
        });
    }

    [Fact]
    public async Task ChannelPublishSubscription_OverChannelLimit_FailsWithoutDisconnecting()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            server.Rider.Options.ChannelLimit = 1;
            await server.Rider.Channel.Create("limit-channel");

            HorseClient client = new HorseClient { AutoSubscribe = false };
            await client.ConnectAsync($"horse://localhost:{port}");

            // the channel cannot be created, the client gets Failed and stays connected
            HorseResult result = await client.Event.Subscribe(HorseEventType.ChannelPublish, "over-limit-channel", true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Failed, result.Code);
            Assert.Null(server.Rider.Channel.Find("over-limit-channel"));

            await Task.Delay(250);
            Assert.True(client.IsConnected);
            Assert.Equal(HorseResultCode.Ok, (await client.Event.Subscribe(HorseEventType.ChannelPublish, "limit-channel", true, CancellationToken.None)).Code);

            client.Disconnect();
        });
    }

    [Fact]
    public async Task SubscribeThroughRemovedChannel_IsRejected()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient { AutoSubscribe = false };
            await client.ConnectAsync($"horse://localhost:{port}");

            await WaitUntil(() => server.Rider.Client.Find(client.ClientId) != null);
            MessagingClient messagingClient = server.Rider.Client.Find(client.ClientId);
            Assert.NotNull(messagingClient);

            HorseChannel channel = await server.Rider.Channel.Create("removed-event-channel");
            server.Rider.Channel.Remove(channel);

            // a subscribe request that found the channel just before it was removed must not leave a subscription
            Assert.False(channel.PublishEvent.Subscribe(messagingClient));
            Assert.Empty(channel.PublishEvent.Subscribers);

            client.Disconnect();
        });
    }

    [Fact]
    public async Task EventsAreReceivedInTriggerOrder()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            ConcurrentQueue<HorseMessage> received = new();
            HorseClient subscriber = CreateEventClient(received);
            await subscriber.ConnectAsync($"horse://localhost:{port}");
            Assert.Equal(HorseResultCode.Ok, (await subscriber.Event.Subscribe(HorseEventType.QueueCreate, null, true, CancellationToken.None)).Code);

            const int count = 200;
            List<string> expected = new();
            for (int i = 0; i < count; i++)
            {
                string name = $"order-queue-{i}";
                expected.Add(name);
                await server.Rider.Queue.Create(name);
            }

            await WaitUntil(() => received.Count >= count);

            List<string> targets = new();
            foreach (HorseMessage message in received)
                targets.Add(message.Target);

            Assert.Equal(expected, targets);
            subscriber.Disconnect();
        });
    }

    [Fact]
    public async Task UnboundedEventBuffer_DeliversInOrderAndStopsOnDisconnect()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            // zero or negative is unlimited: the outbox uses an unbounded single reader channel
            server.Rider.Options.EventBufferSize = 0;

            ConcurrentQueue<HorseMessage> received = new();
            HorseClient subscriber = CreateEventClient(received);
            await subscriber.ConnectAsync($"horse://localhost:{port}");
            Assert.Equal(HorseResultCode.Ok, (await subscriber.Event.Subscribe(HorseEventType.QueueCreate, null, true, CancellationToken.None)).Code);

            const int count = 200;
            List<string> expected = new();
            for (int i = 0; i < count; i++)
            {
                string name = $"unbounded-queue-{i}";
                expected.Add(name);
                await server.Rider.Queue.Create(name);
            }

            await WaitUntil(() => received.Count >= count);

            List<string> targets = new();
            foreach (HorseMessage message in received)
                targets.Add(message.Target);

            Assert.Equal(expected, targets);

            // events triggered while and after the client disconnects are dropped without errors
            subscriber.Disconnect();
            for (int i = 0; i < 50; i++)
                await server.Rider.Queue.Create($"after-disconnect-queue-{i}");

            await WaitUntil(() => server.Rider.Queue.CreateEvent.Subscribers.Count == 0);
            Assert.Empty(server.Rider.Queue.CreateEvent.Subscribers);
        });
    }

    [Fact]
    public async Task SubscribeToUnknownEventType_ReturnsFailed()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient client = new HorseClient { AutoSubscribe = false, ResponseTimeout = TimeSpan.FromSeconds(3) };
            await client.ConnectAsync($"horse://localhost:{port}");

            // previously no response was sent and the client waited until timeout
            HorseResult result = await client.Event.Subscribe((HorseEventType) 9999, null, true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Failed, result.Code);

            client.Disconnect();
        });
    }

    [Fact]
    public async Task QueueStatusChangeEvent_IsTriggeredAfterStatusChanges()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseQueue queue = await server.Rider.Queue.Create("status-queue");

            ConcurrentQueue<HorseMessage> received = new();
            HorseClient subscriber = CreateEventClient(received);
            await subscriber.ConnectAsync($"horse://localhost:{port}");
            Assert.Equal(HorseResultCode.Ok, (await subscriber.Event.Subscribe(HorseEventType.QueueStatusChange, null, true, CancellationToken.None)).Code);

            QueueStatus previous = queue.Status;
            queue.SetStatus(QueueStatus.Paused);

            await WaitUntil(() => !received.IsEmpty);
            Assert.True(received.TryDequeue(out HorseMessage message));

            HorseEvent horseEvent = (HorseEvent) subscriber.MessageSerializer.Deserialize(message, typeof(HorseEvent));
            Assert.Equal(previous.ToString(), horseEvent.FindParameter($"Previous-{HorseHeaders.STATUS}"));
            Assert.Equal(QueueStatus.Paused.ToString(), horseEvent.FindParameter($"next-{HorseHeaders.STATUS}"));

            subscriber.Disconnect();
        });
    }
}
