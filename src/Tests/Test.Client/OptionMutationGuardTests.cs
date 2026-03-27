using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;
using Test.Common;
using Xunit;

namespace Test.Client;

[QueueName("queue-limit-resize-existing")]
[ClientLimit(4)]
internal class QueueResizeExistingConsumer : IQueueConsumer<QueueResizeExistingModel>
{
    public Task Consume(ConsumeContext<QueueResizeExistingModel> context)
    {
        return Task.CompletedTask;
    }
}

internal class QueueResizeExistingModel
{
    public string Value { get; set; }
}

[ChannelName("channel-limit-resize-existing")]
[ClientLimit(4)]
internal class ChannelResizeExistingSubscriber : IChannelSubscriber<string>
{
    public Task Handle(ChannelMessageContext<string> context)
    {
        return Task.CompletedTask;
    }

    public Task Error(Exception exception, ChannelMessageContext<string> context)
    {
        return Task.CompletedTask;
    }
}

[QueueName("auth-message-timeout-queue")]
[MessageTimeout(MessageTimeoutPolicy.Delete, 2)]
internal class AuthMessageTimeoutModel
{
    public string Value { get; set; }
}

[ChannelName("channel-client-limit-header")]
[ClientLimit(3)]
internal class ChannelClientLimitHeaderModel
{
    public string Value { get; set; }
}

internal class ChannelHeaderCaptureSubscriber : IChannelSubscriber<ChannelClientLimitHeaderModel>
{
    public static bool Received { get; private set; }
    public static bool HasClientLimitHeader { get; private set; }

    public static void Reset()
    {
        Received = false;
        HasClientLimitHeader = false;
    }

    public Task Handle(ChannelMessageContext<ChannelClientLimitHeaderModel> context)
    {
        HasClientLimitHeader = context.Message.FindHeader(HorseHeaders.CLIENT_LIMIT) != null;
        Received = true;
        return Task.CompletedTask;
    }

    public Task Error(Exception exception, ChannelMessageContext<ChannelClientLimitHeaderModel> context)
    {
        return Task.CompletedTask;
    }
}

internal class DenyQueueSubscribeAuthenticator : IQueueAuthenticator
{
    private readonly string _queueName;

    public DenyQueueSubscribeAuthenticator(string queueName)
    {
        _queueName = queueName;
    }

    public Task<bool> Authenticate(HorseQueue queue, MessagingClient client)
    {
        return Task.FromResult(!queue.Name.Equals(_queueName, StringComparison.OrdinalIgnoreCase));
    }
}

internal class DenyQueuePushAuthorization : IClientAuthorization
{
    private readonly string _queueName;

    public DenyQueuePushAuthorization(string queueName)
    {
        _queueName = queueName;
    }

    public Task<bool> CanCreateQueue(MessagingClient client, string name, NetworkOptionsBuilder options)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CanDirectMessage(MessagingClient sender, HorseMessage message, MessagingClient receiver)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CanMessageToQueue(MessagingClient client, HorseQueue queue, HorseMessage message)
    {
        return Task.FromResult(!queue.Name.Equals(_queueName, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> CanPullFromQueue(QueueClient client, HorseQueue queue)
    {
        return Task.FromResult(true);
    }

    public bool CanSubscribeEvent(MessagingClient client, HorseEventType eventType, string target)
    {
        return true;
    }

    public Task<bool> CanCreateRouter(MessagingClient client, string routerName, RouteMethod method)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CanRemoveRouter(MessagingClient client, Router router)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CanCreateBinding(MessagingClient client, Router router, BindingInformation binding)
    {
        return Task.FromResult(true);
    }

    public Task<bool> CanRemoveBinding(MessagingClient client, Binding binding)
    {
        return Task.FromResult(true);
    }
}

internal class DenyChannelSubscribeAuthorization : IChannelAuthorization
{
    private readonly string _channelName;

    public DenyChannelSubscribeAuthorization(string channelName)
    {
        _channelName = channelName;
    }

    public bool CanPush(MessagingClient client, HorseMessage message)
    {
        return true;
    }

    public bool CanSubscribe(HorseChannel channel, MessagingClient client)
    {
        return !channel.Name.Equals(_channelName, StringComparison.OrdinalIgnoreCase);
    }
}

public class OptionMutationGuardTests
{
    private static async Task<(TestHorseRider server, int port)> StartServer(Action<TestHorseRider> configure = null)
    {
        var server = new TestHorseRider();
        await server.Initialize();
        configure?.Invoke(server);
        int port = server.Start(300, 300);
        return (server, port);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(30);
    }

    [Fact]
    public async Task QueueSubscribe_Unauthorized_DoesNotUpdateExistingQueueOptions()
    {
        const string queueName = "queue-subscribe-auth-guard";

        var (server, port) = await StartServer(s =>
        {
            s.Rider.Queue.Authenticators.Add(new DenyQueueSubscribeAuthenticator(queueName));
        });

        try
        {
            await server.Rider.Queue.Create(queueName, options =>
            {
                options.Type = QueueType.Push;
                options.ClientLimit = 5;
                options.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
            });

            HorseQueue queue = server.Rider.Queue.Find(queueName);
            queue.Topic = "before-topic";

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Queue.Subscribe(queueName, true, new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.QUEUE_TOPIC, "after-topic"),
                new KeyValuePair<string, string>(HorseHeaders.CLIENT_LIMIT, "2"),
                new KeyValuePair<string, string>(HorseHeaders.ACK_TIMEOUT, "9")
            }, CancellationToken.None);

            Assert.Equal(HorseResultCode.Unauthorized, result.Code);
            Assert.Equal("before-topic", queue.Topic);
            Assert.Equal(5, queue.Options.ClientLimit);
            Assert.Equal(TimeSpan.FromSeconds(2), queue.Options.AcknowledgeTimeout);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueuePush_Unauthorized_DoesNotUpdateExistingMessageTimeout()
    {
        const string queueName = "auth-message-timeout-queue";

        var (server, port) = await StartServer(s =>
        {
            s.Rider.Client.Authorizations.Add(new DenyQueuePushAuthorization(queueName));
        });

        try
        {
            await server.Rider.Queue.Create(queueName, options =>
            {
                options.Type = QueueType.Push;
                options.CommitWhen = CommitWhen.None;
                options.Acknowledge = QueueAckDecision.None;
                options.MessageTimeout = new MessageTimeoutStrategy
                {
                    MessageDuration = 0,
                    Policy = MessageTimeoutPolicy.NoTimeout
                };
            });

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Queue.Push(new AuthMessageTimeoutModel
            {
                Value = "blocked"
            }, true, CancellationToken.None);

            HorseQueue queue = server.Rider.Queue.Find(queueName);

            Assert.Equal(HorseResultCode.Unauthorized, result.Code);
            Assert.NotNull(queue);
            Assert.NotNull(queue.Options.MessageTimeout);
            Assert.Equal(0, queue.Options.MessageTimeout.MessageDuration);
            Assert.Equal(MessageTimeoutPolicy.NoTimeout, queue.Options.MessageTimeout.Policy);
            Assert.True(queue.IsEmpty);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task ChannelSubscribe_Unauthorized_DoesNotUpdateExistingChannelOptions()
    {
        const string channelName = "channel-subscribe-auth-guard";

        var (server, port) = await StartServer(s =>
        {
            s.Rider.Channel.Authenticators.Add(new DenyChannelSubscribeAuthorization(channelName));
        });

        try
        {
            await server.Rider.Channel.Create(channelName, options =>
            {
                options.ClientLimit = 8;
            });

            HorseClient client = new HorseClient();
            await client.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await client.Channel.Subscribe(channelName, true, new[]
            {
                new KeyValuePair<string, string>(HorseHeaders.CLIENT_LIMIT, "2")
            }, CancellationToken.None);

            HorseChannel channel = server.Rider.Channel.Find(channelName);

            Assert.Equal(HorseResultCode.Unauthorized, result.Code);
            Assert.NotNull(channel);
            Assert.Equal(8, channel.Options.ClientLimit);

            client.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task QueueClientLimitIncrease_OnExistingQueue_AllowsAdditionalSubscribers()
    {
        const string queueName = "queue-limit-resize-existing";

        var (server, port) = await StartServer();

        try
        {
            await server.Rider.Queue.Create(queueName, options =>
            {
                options.Type = QueueType.Push;
                options.ClientLimit = 2;
            });

            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonConsumer<QueueResizeExistingConsumer>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await WaitUntil(() => server.Rider.Queue.Find(queueName)?.Options.ClientLimit == 4);

            HorseClient second = new HorseClient();
            HorseClient third = new HorseClient();
            HorseClient fourth = new HorseClient();
            HorseClient fifth = new HorseClient();

            await second.ConnectAsync($"horse://localhost:{port}");
            await third.ConnectAsync($"horse://localhost:{port}");
            await fourth.ConnectAsync($"horse://localhost:{port}");
            await fifth.ConnectAsync($"horse://localhost:{port}");

            HorseResult secondResult = await second.Queue.Subscribe(queueName, true, CancellationToken.None);
            HorseResult thirdResult = await third.Queue.Subscribe(queueName, true, CancellationToken.None);
            HorseResult fourthResult = await fourth.Queue.Subscribe(queueName, true, CancellationToken.None);
            HorseResult fifthResult = await fifth.Queue.Subscribe(queueName, true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, secondResult.Code);
            Assert.Equal(HorseResultCode.Ok, thirdResult.Code);
            Assert.Equal(HorseResultCode.Ok, fourthResult.Code);
            Assert.Equal(HorseResultCode.LimitExceeded, fifthResult.Code);

            second.Disconnect();
            third.Disconnect();
            fourth.Disconnect();
            fifth.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task ChannelClientLimitIncrease_OnExistingChannel_AllowsAdditionalSubscribers()
    {
        const string channelName = "channel-limit-resize-existing";

        var (server, port) = await StartServer();

        try
        {
            await server.Rider.Channel.Create(channelName, options =>
            {
                options.ClientLimit = 2;
            });

            HorseClient consumer = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonChannelSubscriber<ChannelResizeExistingSubscriber>()
                .AutoSubscribe(true)
                .Build();

            await consumer.ConnectAsync();
            await WaitUntil(() => server.Rider.Channel.Find(channelName)?.Options.ClientLimit == 4);

            HorseClient second = new HorseClient();
            HorseClient third = new HorseClient();

            await second.ConnectAsync($"horse://localhost:{port}");
            await third.ConnectAsync($"horse://localhost:{port}");

            HorseResult secondResult = await second.Channel.Subscribe(channelName, true, CancellationToken.None);
            HorseResult thirdResult = await third.Channel.Subscribe(channelName, true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, secondResult.Code);
            Assert.Equal(HorseResultCode.Ok, thirdResult.Code);

            second.Disconnect();
            third.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public async Task ChannelPublish_StripsClientLimitHeaderFromDeliveredMessage()
    {
        ChannelHeaderCaptureSubscriber.Reset();

        var (server, port) = await StartServer();

        try
        {
            HorseClient subscriber = new HorseClientBuilder()
                .AddHost("horse://localhost:" + port)
                .AddSingletonChannelSubscriber<ChannelHeaderCaptureSubscriber>()
                .AutoSubscribe(true)
                .Build();

            await subscriber.ConnectAsync();

            HorseClient publisher = new HorseClient();
            await publisher.ConnectAsync($"horse://localhost:{port}");

            HorseResult result = await publisher.Channel.Publish(new ChannelClientLimitHeaderModel
            {
                Value = "hello"
            }, true, CancellationToken.None);

            Assert.Equal(HorseResultCode.Ok, result.Code);

            await WaitUntil(() => ChannelHeaderCaptureSubscriber.Received);

            Assert.True(ChannelHeaderCaptureSubscriber.Received);
            Assert.False(ChannelHeaderCaptureSubscriber.HasClientLimitHeader);

            publisher.Disconnect();
            subscriber.Disconnect();
        }
        finally
        {
            server.Stop();
        }
    }
}
