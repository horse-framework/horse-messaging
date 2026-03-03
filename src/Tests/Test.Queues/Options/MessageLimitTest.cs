using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

public class MessageLimitTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageLimit_RejectNew_ReturnsError(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("ml-reject", o =>
        {
            o.Type = QueueType.Push;
            o.MessageLimit = 3;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.RejectNewMessage;
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push 3 messages (within limit)
        for (int i = 0; i < 3; i++)
        {
            HorseResult r = await producer.Queue.Push("ml-reject", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);
            Assert.Equal(HorseResultCode.Ok, r.Code);
        }

        // 4th message exceeds limit → should be rejected
        HorseResult rejected = await producer.Queue.Push("ml-reject", new MemoryStream("overflow"u8.ToArray()), true);
        Assert.NotEqual(HorseResultCode.Ok, rejected.Code);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageLimit_DeleteOldest_OldestRemoved(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("ml-oldest", o =>
        {
            o.Type = QueueType.Push;
            o.MessageLimit = 3;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push 5 messages with DeleteOldest strategy
        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("ml-oldest", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);

        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("ml-oldest");
        int count = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.True(count <= 3, $"Expected <=3 messages but found {count}");

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageLimit_Zero_Unlimited(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("ml-unlim", o =>
        {
            o.Type = QueueType.Push;
            o.MessageLimit = 0; // unlimited
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 50; i++)
        {
            HorseResult r = await producer.Queue.Push("ml-unlim", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);
            Assert.Equal(HorseResultCode.Ok, r.Code);
        }

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageLimit_AtBoundary_ExactCountAllowed(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("ml-exact", o =>
        {
            o.Type = QueueType.Push;
            o.MessageLimit = 5;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.RejectNewMessage;
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Exactly 5 messages → all should be accepted
        for (int i = 0; i < 5; i++)
        {
            HorseResult r = await producer.Queue.Push("ml-exact", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);
            Assert.Equal(HorseResultCode.Ok, r.Code);
        }

        HorseQueue queue = ctx.Rider.Queue.Find("ml-exact");
        int count = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(5, count);

        producer.Disconnect();
    }
}

