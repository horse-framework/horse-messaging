using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Subscribe;

public class SubscribeUnsubscribeTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Subscribe_ReturnsOk(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("sub-ok", o => o.Type = QueueType.Push);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult result = await client.Queue.Subscribe("sub-ok", true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Subscribe_Twice_Idempotent(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("sub-twice", o => o.Type = QueueType.Push);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult r1 = await client.Queue.Subscribe("sub-twice", true);
        HorseResult r2 = await client.Queue.Subscribe("sub-twice", true);

        Assert.Equal(HorseResultCode.Ok, r1.Code);
        Assert.Equal(HorseResultCode.Ok, r2.Code);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Unsubscribe_ReturnsOk(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("unsub-ok", o => o.Type = QueueType.Push);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        await client.Queue.Subscribe("unsub-ok", true);
        HorseResult result = await client.Queue.Unsubscribe("unsub-ok", true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Unsubscribe_NotSubscribed_NoError(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("unsub-no", o => o.Type = QueueType.Push);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Unsubscribe without ever subscribing — server should respond without crashing
        HorseResult result = await client.Queue.Unsubscribe("unsub-no", true);
        // Ok or NotFound are both acceptable outcomes
        Assert.True(result.Code == HorseResultCode.Ok || result.Code == HorseResultCode.NotFound,
            $"Expected Ok or NotFound but got {result.Code}");

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task UnsubscribeAll_LeavesAllQueues(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("unsub-all-1", o => o.Type = QueueType.Push);
        await ctx.Rider.Queue.Create("unsub-all-2", o => o.Type = QueueType.Push);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        await client.Queue.Subscribe("unsub-all-1", true);
        await client.Queue.Subscribe("unsub-all-2", true);

        HorseResult result = await client.Queue.UnsubscribeFromAllQueues();
        Assert.Equal(HorseResultCode.Ok, result.Code);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Subscribe_NonExistent_AutoCreates(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Queue doesn't exist, but AutoQueueCreation=true
        HorseResult result = await client.Queue.Subscribe("auto-create-sub", true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        HorseQueue queue = ctx.Rider.Queue.Find("auto-create-sub");
        Assert.NotNull(queue);

        client.Disconnect();
    }
}

