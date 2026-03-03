using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues.Core;

public class QueueStatusTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_Running_PushAndConsumeBothWork(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("status-run", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("status-run", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("status-run", new MemoryStream("hello"u8.ToArray()), true);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_Paused_BothBlocked(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseQueue queue = await ctx.Rider.Queue.Create("status-pause", o => o.Type = QueueType.Push);
        queue.SetStatus(QueueStatus.Paused);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("status-pause", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push while paused
        await producer.Queue.Push("status-pause", new MemoryStream("paused"u8.ToArray()), false);
        await Task.Delay(1000);

        // Consumer should NOT receive message while paused
        Assert.Null(received);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_OnlyPush_ProducerCanPush_ConsumerNotReceive(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseQueue queue = await ctx.Rider.Queue.Create("status-op", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("status-op", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        queue.SetStatus(QueueStatus.OnlyPush);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("status-op", new MemoryStream("push-only"u8.ToArray()), false);
        await Task.Delay(1000);

        // Message stored but NOT delivered while in OnlyPush
        Assert.Null(received);
        Assert.False(queue.IsEmpty);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_ChangeAtRuntime_TakesEffect(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseQueue queue = await ctx.Rider.Queue.Create("status-change", o => o.Type = QueueType.Push);

        Assert.Equal(QueueStatus.Running, queue.Status);

        queue.SetStatus(QueueStatus.Paused);
        Assert.Equal(QueueStatus.Paused, queue.Status);

        queue.SetStatus(QueueStatus.Running);
        Assert.Equal(QueueStatus.Running, queue.Status);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_BackToRunning_ResumesBoth(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseQueue queue = await ctx.Rider.Queue.Create("status-resume", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("status-resume", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        // Pause, push, then resume
        queue.SetStatus(QueueStatus.OnlyPush);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("status-resume", new MemoryStream("resume-me"u8.ToArray()), false);
        await Task.Delay(500);
        Assert.Null(received);

        // Resume → pending message should be delivered
        queue.SetStatus(QueueStatus.Running);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);

        producer.Disconnect();
        consumer.Disconnect();
    }
}

