using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

public class CommitTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Commit_None_ProducerNoBlock(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("commit-none", o => o.Type = QueueType.Push);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            // waitForCommit=false with CommitWhen.None → fast return
            HorseResult result = await producer.Queue.Push("commit-none", new MemoryStream("msg"u8.ToArray()), false, CancellationToken.None);
            Assert.NotNull(result);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Commit_AfterReceived_ProducerGetsOk(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("commit-recv", o => o.Type = QueueType.Push);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            HorseResult result = await producer.Queue.Push("commit-recv", new MemoryStream("msg"u8.ToArray()), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Commit_AfterSent_ProducerGetsOkAfterDelivery(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterSent;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("commit-sent", o => o.Type = QueueType.Push);

            // Need at least one subscriber for "AfterSent" to resolve
            HorseClient consumer = new HorseClient();
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("commit-sent", true, CancellationToken.None);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            HorseResult result = await producer.Queue.Push("commit-sent", new MemoryStream("msg"u8.ToArray()), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

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
    public async Task Commit_AfterAcknowledge_ProducerWaitsForAck(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });
        try
        {

            await ctx.Rider.Queue.Create("commit-ack", o => o.Type = QueueType.RoundRobin);

            // Consumer with auto-ack so the ack is sent back
            HorseClient consumer = new HorseClient();
            consumer.AutoAcknowledge = true;
            await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
            await consumer.Queue.Subscribe("commit-ack", true, CancellationToken.None);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            HorseResult result = await producer.Queue.Push("commit-ack", new MemoryStream("msg"u8.ToArray()), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

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
    public async Task Commit_WaitForCommit_FalseOnClient_NoBlock(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });
        try
        {

            await ctx.Rider.Queue.Create("commit-noblock", o => o.Type = QueueType.RoundRobin);

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            // waitForCommit=false → producer doesn't wait regardless of server setting
            HorseResult result = await producer.Queue.Push("commit-noblock", new MemoryStream("msg"u8.ToArray()), false, CancellationToken.None);
            Assert.NotNull(result);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }
}

