using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues.Core;

public class QueueCreateTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_ExplicitPush(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("push-q", o => o.Type = QueueType.Push);
        HorseQueue queue = ctx.Rider.Queue.Find("push-q");

        Assert.NotNull(queue);
        Assert.Equal(QueueType.Push, queue.Type);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_ExplicitRoundRobin(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("rr-q", o => o.Type = QueueType.RoundRobin);
        HorseQueue queue = ctx.Rider.Queue.Find("rr-q");

        Assert.NotNull(queue);
        Assert.Equal(QueueType.RoundRobin, queue.Type);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_ExplicitPull(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("pull-q", o => o.Type = QueueType.Pull);
        HorseQueue queue = ctx.Rider.Queue.Find("pull-q");

        Assert.NotNull(queue);
        Assert.Equal(QueueType.Pull, queue.Type);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_AutoQueueCreation_OnPush(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        await client.Queue.Push("auto-push-q", new MemoryStream("hello"u8.ToArray()), true, CancellationToken.None);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("auto-push-q");
        Assert.NotNull(queue);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_AutoQueueCreation_OnSubscribe(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        Assert.True(client.IsConnected);

        HorseResult result = await client.Queue.Subscribe("auto-sub-q", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        HorseQueue queue = ctx.Rider.Queue.Find("auto-sub-q");
        Assert.NotNull(queue);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_Duplicate_ThrowsDuplicateNameException(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dup-q", o => o.Type = QueueType.Push);

        await Assert.ThrowsAsync<System.Data.DuplicateNameException>(async () =>
            await ctx.Rider.Queue.Create("dup-q", o => o.Type = QueueType.Push));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_WithOptions_OptionsApplied(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("opt-q", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.MessageLimit = 100;
            o.ClientLimit = 5;
            o.DelayBetweenMessages = 50;
        });

        HorseQueue queue = ctx.Rider.Queue.Find("opt-q");
        Assert.NotNull(queue);
        Assert.Equal(QueueType.RoundRobin, queue.Type);
        Assert.Equal(100, queue.Options.MessageLimit);
        Assert.Equal(5, queue.Options.ClientLimit);
        Assert.Equal(50, queue.Options.DelayBetweenMessages);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_WithClientQueueOperator_AppliesTopicAndPutBack(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult result = await client.Queue.Create("client-create-q", options =>
        {
            options.Type = MessagingQueueType.RoundRobin;
            options.Topic = "client-topic";
            options.PutBack = PutBack.Priority;
            options.PutBackDelay = 1500;
            options.ClientLimit = 2;
            options.DelayBetweenMessages = 25;
            options.AcknowledgeTimeout = 9000;
            options.MessageTimeout = new MessageTimeoutStrategyInfo(17, "delete");
        }, null, null, CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);

        HorseQueue queue = ctx.Rider.Queue.Find("client-create-q");
        Assert.NotNull(queue);
        Assert.Equal(QueueType.RoundRobin, queue.Type);
        Assert.Equal("client-topic", queue.Topic);
        Assert.Equal(PutBackDecision.Priority, queue.Options.PutBack);
        Assert.Equal(1500, queue.Options.PutBackDelay);
        Assert.Equal(2, queue.Options.ClientLimit);
        Assert.Equal(25, queue.Options.DelayBetweenMessages);
        Assert.Equal(TimeSpan.FromMilliseconds(9000), queue.Options.AcknowledgeTimeout);
        Assert.Equal(17, queue.Options.MessageTimeout.MessageDuration);
        Assert.Equal(MessageTimeoutPolicy.Delete, queue.Options.MessageTimeout.Policy);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SetOptions_WithClientQueueOperator_UpdatesTopicAndPutBack(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("client-update-q", options =>
        {
            options.Type = QueueType.Push;
            options.PutBack = PutBackDecision.No;
            options.PutBackDelay = 0;
            options.ClientLimit = 5;
            options.DelayBetweenMessages = 0;
            options.AcknowledgeTimeout = TimeSpan.FromSeconds(1);
            options.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = 0,
                Policy = MessageTimeoutPolicy.NoTimeout,
                TargetName = string.Empty
            };
        });

        HorseQueue existing = ctx.Rider.Queue.Find("client-update-q");
        Assert.NotNull(existing);
        existing.Topic = "before-topic";

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult result = await client.Queue.SetOptions("client-update-q", options =>
        {
            options.Topic = "after-topic";
            options.PutBack = PutBack.Regular;
            options.PutBackDelay = 2200;
            options.ClientLimit = 1;
            options.DelayBetweenMessages = 15;
            options.AcknowledgeTimeout = 4000;
            options.MessageTimeout = new MessageTimeoutStrategyInfo(9, "delete");
            options.Type = MessagingQueueType.RoundRobin;
        }, CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);

        HorseQueue queue = ctx.Rider.Queue.Find("client-update-q");
        Assert.NotNull(queue);
        Assert.Equal(QueueType.Push, queue.Type);
        Assert.Equal("after-topic", queue.Topic);
        Assert.Equal(PutBackDecision.Regular, queue.Options.PutBack);
        Assert.Equal(2200, queue.Options.PutBackDelay);
        Assert.Equal(1, queue.Options.ClientLimit);
        Assert.Equal(15, queue.Options.DelayBetweenMessages);
        Assert.Equal(TimeSpan.FromMilliseconds(4000), queue.Options.AcknowledgeTimeout);
        Assert.Equal(9, queue.Options.MessageTimeout.MessageDuration);
        Assert.Equal(MessageTimeoutPolicy.Delete, queue.Options.MessageTimeout.Policy);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Find_NonExistent_ReturnsNull(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseQueue queue = ctx.Rider.Queue.Find("does-not-exist");
        Assert.Null(queue);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_WithTopic_TopicPreserved(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("topic-q", o => o.Type = QueueType.Push);
        HorseQueue queue = ctx.Rider.Queue.Find("topic-q");
        queue.Topic = "my-topic";

        Assert.Equal("my-topic", queue.Topic);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Find_IsCaseSensitive(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("CaseSensitive-Q", o => o.Type = QueueType.Push);

        HorseQueue found = ctx.Rider.Queue.Find("CaseSensitive-Q");
        Assert.NotNull(found);

        HorseQueue notFound = ctx.Rider.Queue.Find("casesensitive-q");
        Assert.Null(notFound);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_MultipleTypes_Coexist(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("push-q2", o => o.Type = QueueType.Push);
        await ctx.Rider.Queue.Create("rr-q2", o => o.Type = QueueType.RoundRobin);
        await ctx.Rider.Queue.Create("pull-q2", o => o.Type = QueueType.Pull);

        Assert.NotNull(ctx.Rider.Queue.Find("push-q2"));
        Assert.NotNull(ctx.Rider.Queue.Find("rr-q2"));
        Assert.NotNull(ctx.Rider.Queue.Find("pull-q2"));
        Assert.Equal(QueueType.Push, ctx.Rider.Queue.Find("push-q2").Type);
        Assert.Equal(QueueType.RoundRobin, ctx.Rider.Queue.Find("rr-q2").Type);
        Assert.Equal(QueueType.Pull, ctx.Rider.Queue.Find("pull-q2").Type);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_AutoQueueCreation_Disabled_SubscribeDoesNotCreate(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.AutoQueueCreation = false;
        });

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult result = await client.Queue.Subscribe("no-auto-q", true, CancellationToken.None);
        // When auto creation is off, subscribe should not silently create
        HorseQueue queue = ctx.Rider.Queue.Find("no-auto-q");
        Assert.Null(queue);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Create_InvalidName_WithSpecialChars_Rejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            async () => await ctx.Rider.Queue.Create("bad@name"));

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            async () => await ctx.Rider.Queue.Create("bad name"));

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            async () => await ctx.Rider.Queue.Create("bad;name"));

        await Assert.ThrowsAsync<System.InvalidOperationException>(
            async () => await ctx.Rider.Queue.Create("bad*name"));
    }
}
