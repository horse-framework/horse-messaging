using System.IO;
using System.Threading;
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
        try
        {

            await ctx.Rider.Queue.Create("status-run", o => o.Type = QueueType.Push);

            HorseClient consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("status-run", true, CancellationToken.None);

            HorseMessage received = null;
            consumer.MessageReceived += (_, m) => received = m;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await producer.Queue.Push("status-run", new MemoryStream("hello"u8.ToArray()), true, CancellationToken.None);

            for (int i = 0; i < 30 && received == null; i++)
                await Task.Delay(100);

            Assert.NotNull(received);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
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
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-pause", o => o.Type = QueueType.Push);
            queue.SetStatus(QueueStatus.Paused);

            HorseClient consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("status-pause", true, CancellationToken.None);

            HorseMessage received = null;
            consumer.MessageReceived += (_, m) => received = m;

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            // Push while paused
            await producer.Queue.Push("status-pause", new MemoryStream("paused"u8.ToArray()), false, CancellationToken.None);
            await Task.Delay(1000);

            // Consumer should NOT receive message while paused
            Assert.Null(received);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
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
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-op", o => o.Type = QueueType.Push);

            HorseClient consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("status-op", true, CancellationToken.None);

            HorseMessage received = null;
            consumer.MessageReceived += (_, m) => received = m;

            queue.SetStatus(QueueStatus.OnlyPush);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await producer.Queue.Push("status-op", new MemoryStream("push-only"u8.ToArray()), false, CancellationToken.None);
            await Task.Delay(1000);

            // Message stored but NOT delivered while in OnlyPush
            Assert.Null(received);
            Assert.False(queue.IsEmpty);

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
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
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-change", o => o.Type = QueueType.Push);

            Assert.Equal(QueueStatus.Running, queue.Status);

            queue.SetStatus(QueueStatus.Paused);
            Assert.Equal(QueueStatus.Paused, queue.Status);

            queue.SetStatus(QueueStatus.Running);
            Assert.Equal(QueueStatus.Running, queue.Status);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
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
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-resume", o => o.Type = QueueType.Push);

            HorseClient consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("status-resume", true, CancellationToken.None);

            HorseMessage received = null;
            consumer.MessageReceived += (_, m) => received = m;

            // Pause, push, then resume
            queue.SetStatus(QueueStatus.OnlyPush);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await producer.Queue.Push("status-resume", new MemoryStream("resume-me"u8.ToArray()), false, CancellationToken.None);
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
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_OnlyConsume_ConsumerReceivesStored_ProducerRejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-oc", o => o.Type = QueueType.Push);

            // Push while running so messages get stored
            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await producer.Queue.Push("status-oc", new MemoryStream("before-oc"u8.ToArray()), false, CancellationToken.None);
            await Task.Delay(300);

            // Switch to OnlyConsume
            queue.SetStatus(QueueStatus.OnlyConsume);

            // Now subscribe → stored messages should be delivered
            HorseMessage received = null;
            HorseClient consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            consumer.MessageReceived += (_, m) => received = m;
            await consumer.Queue.Subscribe("status-oc", true, CancellationToken.None);

            for (int i = 0; i < 30 && received == null; i++)
                await Task.Delay(100);

            Assert.NotNull(received);
            Assert.Equal("before-oc", received.ToString());

            producer.Disconnect();
            consumer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_SetToNotInitialized_Ignored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-noinit", o => o.Type = QueueType.Push);
            Assert.Equal(QueueStatus.Running, queue.Status);

            queue.SetStatus(QueueStatus.NotInitialized);
            Assert.Equal(QueueStatus.Running, queue.Status);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_SameStatus_NoOp(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-same", o => o.Type = QueueType.Push);
            Assert.Equal(QueueStatus.Running, queue.Status);

            // Setting same status should be no-op (no exception, no change)
            queue.SetStatus(QueueStatus.Running);
            Assert.Equal(QueueStatus.Running, queue.Status);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Status_OnlyConsume_NewPush_Rejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            HorseQueue queue = await ctx.Rider.Queue.Create("status-oc-reject", o => o.Type = QueueType.Push);
            queue.SetStatus(QueueStatus.OnlyConsume);

            PushResult result = await queue.Push("rejected-msg");
            Assert.Equal(PushResult.StatusNotSupported, result);
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }
}
