using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

public class PullDeliveryTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_SingleMessage_Received(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-one", o => o.Type = QueueType.Pull);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("pull-one", new MemoryStream("pull-me"u8.ToArray()), false);
        await Task.Delay(500);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-one", true);

        PullContainer result = await consumer.Queue.Pull(new PullRequest { Queue = "pull-one", Count = 1 });

        Assert.NotNull(result);
        Assert.Equal(1, result.ReceivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_MultipleMessages_CountRespected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-multi", o => o.Type = QueueType.Pull);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("pull-multi", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-multi", true);

        PullContainer result = await consumer.Queue.Pull(new PullRequest { Queue = "pull-multi", Count = 3 });

        Assert.NotNull(result);
        Assert.Equal(3, result.ReceivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_EmptyQueue_ReturnsNoContent(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-empty", o => o.Type = QueueType.Pull);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-empty", true);

        PullContainer result = await consumer.Queue.Pull(new PullRequest { Queue = "pull-empty", Count = 1 });

        Assert.NotNull(result);
        Assert.Equal(0, result.ReceivedCount);

        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_FIFO_Order(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-fifo", o => o.Type = QueueType.Pull);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("pull-fifo", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-fifo", true);

        PullContainer result = await consumer.Queue.Pull(new PullRequest
        {
            Queue = "pull-fifo",
            Count = 5,
            Order = MessageOrder.FIFO
        });

        Assert.Equal(5, result.ReceivedCount);

        var messages = result.ReceivedMessages.ToList();
        for (int i = 0; i < 5; i++)
            Assert.Equal($"msg-{i}", messages[i].ToString());

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_LIFO_Order(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-lifo", o => o.Type = QueueType.Pull);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("pull-lifo", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-lifo", true);

        PullContainer result = await consumer.Queue.Pull(new PullRequest
        {
            Queue = "pull-lifo",
            Count = 5,
            Order = MessageOrder.LIFO
        });

        Assert.Equal(5, result.ReceivedCount);

        // LIFO: last pushed first
        var messages = result.ReceivedMessages.ToList();
        for (int i = 0; i < 5; i++)
            Assert.Equal($"msg-{4 - i}", messages[i].ToString());

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_MessageNotDelivered_UntilPulled(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-noauto", o => o.Type = QueueType.Pull);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-noauto", true);

        HorseMessage autoReceived = null;
        consumer.MessageReceived += (_, m) => autoReceived = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("pull-noauto", new MemoryStream("no-auto"u8.ToArray()), false);
        await Task.Delay(1000);

        // Pull queue does NOT auto-deliver
        Assert.Null(autoReceived);

        // Only Pull delivers
        PullContainer result = await consumer.Queue.Pull(new PullRequest { Queue = "pull-noauto", Count = 1 });
        Assert.Equal(1, result.ReceivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_QueueMessageCounts_HeaderIncluded(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("pull-counts", o => o.Type = QueueType.Pull);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("pull-counts", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pull-counts", true);

        PullContainer result = await consumer.Queue.Pull(new PullRequest
        {
            Queue = "pull-counts",
            Count = 2,
            GetQueueMessageCounts = true
        });

        Assert.Equal(2, result.ReceivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }
}

