using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
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
