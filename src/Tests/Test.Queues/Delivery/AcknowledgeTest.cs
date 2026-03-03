using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

public class AcknowledgeTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_None_NoAckExpected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("ack-none", o => o.Type = QueueType.Push);

        HorseMessage received = null;
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("ack-none", true);
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("ack-none", new MemoryStream("no-ack"u8.ToArray()), true);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        Assert.Equal("no-ack", received.ToString());

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_JustRequest_AckHeaderSent_NoBlock(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.JustRequest;
        });

        await ctx.Rider.Queue.Create("ack-request", o => o.Type = QueueType.RoundRobin);

        int receivedCount = 0;
        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = true;
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("ack-request", true);
        consumer.MessageReceived += (_, _) => receivedCount++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push 3 messages rapidly — JustRequest means no blocking between messages
        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("ack-request", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);

        for (int i = 0; i < 30 && receivedCount < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, receivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_WaitForAck_NextMessageBlockedUntilAck(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("ack-wait", o => o.Type = QueueType.RoundRobin);

        int receivedCount = 0;
        HorseClient consumer = new HorseClient();
        // DO NOT auto-acknowledge — we want to control when ack is sent
        consumer.AutoAcknowledge = false;
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("ack-wait", true);

        HorseMessage lastReceived = null;
        consumer.MessageReceived += (_, m) =>
        {
            receivedCount++;
            lastReceived = m;
        };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push 2 messages
        await producer.Queue.Push("ack-wait", new MemoryStream("first"u8.ToArray()), false);
        await producer.Queue.Push("ack-wait", new MemoryStream("second"u8.ToArray()), false);

        // Wait for first message to arrive
        for (int i = 0; i < 30 && receivedCount < 1; i++)
            await Task.Delay(100);

        Assert.Equal(1, receivedCount);

        // Second message is blocked until we ack the first
        await Task.Delay(500);
        Assert.Equal(1, receivedCount);

        // Send acknowledge for first message
        Assert.NotNull(lastReceived);
        HorseMessage ack = lastReceived.CreateAcknowledge();
        await consumer.SendAsync(ack);

        // Now second message should arrive
        for (int i = 0; i < 30 && receivedCount < 2; i++)
            await Task.Delay(100);

        Assert.Equal(2, receivedCount);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_PositiveAck_MessageRemoved(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("ack-remove", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.Regular;
        });

        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = true;
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("ack-remove", true);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("ack-remove", new MemoryStream("acked"u8.ToArray()), true);
        await Task.Delay(1000);

        // After ack, message should be removed from store
        HorseQueue queue = ctx.Rider.Queue.Find("ack-remove");
        Assert.True(queue.IsEmpty);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_NegativeAck_MessageHandled(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("ack-neg", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.Regular;
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = false;
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("ack-neg", true);
        consumer.MessageReceived += (_, m) =>
        {
            receivedCount++;
            // Send negative ack
            HorseMessage nack = m.CreateAcknowledge("rejected");
            consumer.SendAsync(nack).GetAwaiter().GetResult();
        };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("ack-neg", new MemoryStream("nacked"u8.ToArray()), false);

        // With PutBack=Regular and negative ack, message is put back and re-delivered
        // Wait for at least 2 deliveries (original + put-back)
        for (int i = 0; i < 50 && receivedCount < 2; i++)
            await Task.Delay(100);

        Assert.True(receivedCount >= 2);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_WaitForAck_PushType_BlocksNextMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("ack-block-push", o => o.Type = QueueType.Push);

        List<string> received = new List<string>();
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) =>
        {
            lock (received) received.Add(m.ToString());
            // Ack the first message only after delay
            if (m.ToString() == "msg-0")
            {
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    await consumer.SendAsync(m.CreateAcknowledge());
                });
            }
            else
            {
                consumer.SendAsync(m.CreateAcknowledge()).GetAwaiter().GetResult();
            }
        };
        await consumer.Queue.Subscribe("ack-block-push", true);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push two messages rapidly
        await producer.Queue.Push("ack-block-push", new MemoryStream("msg-0"u8.ToArray()), false);
        await producer.Queue.Push("ack-block-push", new MemoryStream("msg-1"u8.ToArray()), false);

        // Wait for both to be delivered
        for (int i = 0; i < 50 && received.Count < 2; i++)
            await Task.Delay(100);

        Assert.Equal(2, received.Count);
        Assert.Equal("msg-0", received[0]);
        Assert.Equal("msg-1", received[1]);

        producer.Disconnect();
        consumer.Disconnect();
    }
}

