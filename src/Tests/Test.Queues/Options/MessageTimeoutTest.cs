using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

[QueueName("mt-attr-create")]
[MessageTimeout(MessageTimeoutPolicy.Delete, 7)]
internal class MessageTimeoutCreateModel
{
    public string Value { get; set; }
}

[QueueName("mt-attr-update")]
[MessageTimeout(MessageTimeoutPolicy.Delete, 2)]
internal class MessageTimeoutUpdateModel
{
    public string Value { get; set; }
}

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
        await producer.Queue.Push("mt-delete", new MemoryStream("expires"u8.ToArray()), false, CancellationToken.None);
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
        await producer.Queue.Push("mt-none", new MemoryStream("survives"u8.ToArray()), false, CancellationToken.None);

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
        await producer.Queue.Push("mt-dur", new MemoryStream("still-alive"u8.ToArray()), false, CancellationToken.None);

        await Task.Delay(2000);

        HorseQueue queue = ctx.Rider.Queue.Find("mt-dur");
        Assert.NotNull(queue);
        Assert.False(queue.IsEmpty, "Message should still exist before timeout");

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageTimeoutAttribute_OnPush_AutoCreatesQueueWithConfiguredTimeout(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult result = await producer.Queue.Push(new MessageTimeoutCreateModel
        {
            Value = "create"
        }, false, CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);

        HorseQueue queue = null;
        for (int i = 0; i < 25 && queue == null; i++)
        {
            queue = ctx.Rider.Queue.Find("mt-attr-create");
            if (queue == null)
                await Task.Delay(100);
        }

        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.MessageTimeout);
        Assert.Equal(7, queue.Options.MessageTimeout.MessageDuration);
        Assert.Equal(MessageTimeoutPolicy.Delete, queue.Options.MessageTimeout.Policy);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageTimeoutAttribute_OnPush_UpdatesExistingQueueAndAffectsPushedMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("mt-attr-update", o =>
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

        HorseResult result = await producer.Queue.Push(new MessageTimeoutUpdateModel
        {
            Value = "update"
        }, false, CancellationToken.None);

        Assert.Equal(HorseResultCode.Ok, result.Code);

        HorseQueue queue = ctx.Rider.Queue.Find("mt-attr-update");
        for (int i = 0; i < 25; i++)
        {
            queue = ctx.Rider.Queue.Find("mt-attr-update");
            if (queue?.Options.MessageTimeout != null && queue.Options.MessageTimeout.MessageDuration == 2)
                break;

            await Task.Delay(100);
        }

        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.MessageTimeout);
        Assert.Equal(2, queue.Options.MessageTimeout.MessageDuration);
        Assert.Equal(MessageTimeoutPolicy.Delete, queue.Options.MessageTimeout.Policy);
        Assert.False(queue.IsEmpty);

        for (int i = 0; i < 50 && !queue.IsEmpty; i++)
            await Task.Delay(200);

        Assert.True(queue.IsEmpty, "Message should expire using the timeout from MessageTimeoutAttribute");

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task MessageTimeout_OnPush_UpdatesExistingCcQueueAndAffectsItsMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("mt-cc-main", o =>
        {
            o.Type = QueueType.Push;
            o.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = 0,
                Policy = MessageTimeoutPolicy.NoTimeout
            };
        });

        await ctx.Rider.Queue.Create("mt-cc-target", o =>
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

        await producer.Queue.Push("mt-cc-main", new MemoryStream("cc-update"u8.ToArray()), false, new[]
        {
            new KeyValuePair<string, string>(HorseHeaders.CC, "mt-cc-target"),
            new KeyValuePair<string, string>(HorseHeaders.MESSAGE_TIMEOUT, "2;delete")
        }, CancellationToken.None);

        HorseQueue queue = ctx.Rider.Queue.Find("mt-cc-target");
        for (int i = 0; i < 25; i++)
        {
            queue = ctx.Rider.Queue.Find("mt-cc-target");
            if (queue?.Options.MessageTimeout != null && queue.Options.MessageTimeout.MessageDuration == 2)
                break;

            await Task.Delay(100);
        }

        Assert.NotNull(queue);
        Assert.NotNull(queue.Options.MessageTimeout);
        Assert.Equal(2, queue.Options.MessageTimeout.MessageDuration);
        Assert.Equal(MessageTimeoutPolicy.Delete, queue.Options.MessageTimeout.Policy);
        Assert.False(queue.IsEmpty);

        for (int i = 0; i < 50 && !queue.IsEmpty; i++)
            await Task.Delay(200);

        Assert.True(queue.IsEmpty, "CC queue message should expire using the timeout from push headers");

        producer.Disconnect();
    }
}
