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

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Disconnect_DropsSubscription(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("disc-sub", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("disc-sub", true);
        await Task.Delay(300);

        HorseQueue queue = ctx.Rider.Queue.Find("disc-sub");
        Assert.True(queue.HasAnyClient());

        consumer.Disconnect();
        await Task.Delay(500);

        Assert.False(queue.HasAnyClient());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Subscribe_Unsubscribe_Resubscribe_Works(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("resub-q", o => o.Type = QueueType.Push);

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Subscribe
        await client.Queue.Subscribe("resub-q", true);

        // Unsubscribe
        await client.Queue.Unsubscribe("resub-q", true);

        // Re-subscribe → should receive messages
        int received = 0;
        client.MessageReceived += (_, _) => received++;
        await client.Queue.Subscribe("resub-q", true);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("resub-q", new MemoryStream("hello"u8.ToArray()), true);

        for (int i = 0; i < 30 && received == 0; i++)
            await Task.Delay(100);

        Assert.Equal(1, received);

        client.Disconnect();
        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Subscribe_MultipleQueues_IndependentSubscriptions(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("multi-q1", o => o.Type = QueueType.Push);
        await ctx.Rider.Queue.Create("multi-q2", o => o.Type = QueueType.Push);

        int fromQ1 = 0, fromQ2 = 0;
        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        client.MessageReceived += (_, m) =>
        {
            if (m.Target == "multi-q1") System.Threading.Interlocked.Increment(ref fromQ1);
            if (m.Target == "multi-q2") System.Threading.Interlocked.Increment(ref fromQ2);
        };

        await client.Queue.Subscribe("multi-q1", true);
        await client.Queue.Subscribe("multi-q2", true);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("multi-q1", new MemoryStream("q1"u8.ToArray()), true);
        await producer.Queue.Push("multi-q2", new MemoryStream("q2"u8.ToArray()), true);

        for (int i = 0; i < 30 && (fromQ1 < 1 || fromQ2 < 1); i++)
            await Task.Delay(100);

        Assert.Equal(1, fromQ1);
        Assert.Equal(1, fromQ2);

        client.Disconnect();
        producer.Disconnect();
    }
}
