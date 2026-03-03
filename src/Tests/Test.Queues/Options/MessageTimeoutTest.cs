using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

public class MessageTimeoutTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageTimeout_Delete_MessageRemovedAfterExpiry(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("mt-delete", o =>
        {
            o.Type = QueueType.Push;
            o.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = 2,
                Policy = MessageTimeoutPolicy.Delete
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("mt-delete", new MemoryStream("expires"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("mt-delete");
        Assert.NotNull(queue);
        Assert.False(queue.IsEmpty);

        // Wait for message to expire (2s duration + checker interval buffer)
        for (int i = 0; i < 50 && !queue.IsEmpty; i++)
            await Task.Delay(200);

        Assert.True(queue.IsEmpty, "Message should have been deleted after timeout");

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageTimeout_NoTimeout_MessageSurvives(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("mt-none", o =>
        {
            o.Type = QueueType.Push;
            o.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = 0,
                Policy = MessageTimeoutPolicy.NoTimeout
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("mt-none", new MemoryStream("survives"u8.ToArray()), false);

        // Wait 3 seconds — message should still be there
        await Task.Delay(3000);

        HorseQueue queue = ctx.Rider.Queue.Find("mt-none");
        Assert.NotNull(queue);
        Assert.False(queue.IsEmpty);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageTimeout_Duration_Respected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("mt-dur", o =>
        {
            o.Type = QueueType.Push;
            o.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = 10, // 10 seconds — message should still be alive at 2s
                Policy = MessageTimeoutPolicy.Delete
            };
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("mt-dur", new MemoryStream("still-alive"u8.ToArray()), false);

        await Task.Delay(2000);

        HorseQueue queue = ctx.Rider.Queue.Find("mt-dur");
        Assert.NotNull(queue);
        Assert.False(queue.IsEmpty, "Message should still exist before timeout");

        producer.Disconnect();
    }
}

