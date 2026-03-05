using System;
using System.Collections.Generic;
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
        await consumer.Queue.Subscribe("ack-none", true, CancellationToken.None);
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("ack-none", new MemoryStream("no-ack"u8.ToArray()), true, CancellationToken.None);

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
        await consumer.Queue.Subscribe("ack-request", true, CancellationToken.None);
        consumer.MessageReceived += (_, _) => receivedCount++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push 3 messages rapidly — JustRequest means no blocking between messages
        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("ack-request", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true, CancellationToken.None);

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
        await consumer.Queue.Subscribe("ack-wait", true, CancellationToken.None);

        HorseMessage lastReceived = null;
        consumer.MessageReceived += (_, m) =>
        {
            receivedCount++;
            lastReceived = m;
        };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push 2 messages
        await producer.Queue.Push("ack-wait", new MemoryStream("first"u8.ToArray()), false, CancellationToken.None);
        await producer.Queue.Push("ack-wait", new MemoryStream("second"u8.ToArray()), false, CancellationToken.None);

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
        await consumer.SendAsync(ack, CancellationToken.None);

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
        await consumer.Queue.Subscribe("ack-remove", true, CancellationToken.None);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("ack-remove", new MemoryStream("acked"u8.ToArray()), true, CancellationToken.None);
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
        await consumer.Queue.Subscribe("ack-neg", true, CancellationToken.None);
        consumer.MessageReceived += (_, m) =>
        {
            receivedCount++;
            // Send negative ack
            HorseMessage nack = m.CreateAcknowledge("rejected");
            consumer.SendAsync(nack, CancellationToken.None).GetAwaiter().GetResult();
        };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("ack-neg", new MemoryStream("nacked"u8.ToArray()), false, CancellationToken.None);

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
                    await consumer.SendAsync(m.CreateAcknowledge(), CancellationToken.None);
                });
            }
            else
            {
                consumer.SendAsync(m.CreateAcknowledge(), CancellationToken.None).GetAwaiter().GetResult();
            }
        };
        await consumer.Queue.Subscribe("ack-block-push", true, CancellationToken.None);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push two messages rapidly
        await producer.Queue.Push("ack-block-push", new MemoryStream("msg-0"u8.ToArray()), false, CancellationToken.None);
        await producer.Queue.Push("ack-block-push", new MemoryStream("msg-1"u8.ToArray()), false, CancellationToken.None);

        // Wait for both to be delivered
        for (int i = 0; i < 50 && received.Count < 2; i++)
            await Task.Delay(100);

        Assert.Equal(2, received.Count);
        Assert.Equal("msg-0", received[0]);
        Assert.Equal("msg-1", received[1]);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Ack_WaitForAck_Push_MultipleConsumers_NextMessageReleasedAfterFirstAck(string mode)
    {
        // Push + WaitForAcknowledge + multiple consumers:
        // The message is sent to ALL consumers (fan-out).
        // The queue uses a single TaskCompletionSource for the ack lock.
        // The FIRST consumer ack releases the lock and the next message is dispatched.
        // It does NOT wait for ALL consumers to ack.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
        });

        await ctx.Rider.Queue.Create("ack-push-multi", o => o.Type = QueueType.Push);

        var c1Messages = new List<string>();
        var c2Messages = new List<string>();
        HorseMessage c1Pending = null;
        HorseMessage c2Pending = null;

        // Consumer 1: holds ack for first message
        HorseClient consumer1 = new HorseClient();
        consumer1.ClientId = "c1";
        await consumer1.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer1.MessageReceived += (_, m) =>
        {
            string body = m.GetStringContent();
            lock (c1Messages) c1Messages.Add(body);
            if (body == "msg-0")
                c1Pending = m; // hold ack
            else
                consumer1.SendAsync(m.CreateAcknowledge(), CancellationToken.None).GetAwaiter().GetResult();
        };
        await consumer1.Queue.Subscribe("ack-push-multi", true, CancellationToken.None);

        // Consumer 2: holds ack for first message
        HorseClient consumer2 = new HorseClient();
        consumer2.ClientId = "c2";
        await consumer2.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer2.MessageReceived += (_, m) =>
        {
            string body = m.GetStringContent();
            lock (c2Messages) c2Messages.Add(body);
            if (body == "msg-0")
                c2Pending = m; // hold ack
            else
                consumer2.SendAsync(m.CreateAcknowledge(), CancellationToken.None).GetAwaiter().GetResult();
        };
        await consumer2.Queue.Subscribe("ack-push-multi", true, CancellationToken.None);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push first message — delivered to both consumers (fan-out)
        await producer.Queue.Push("ack-push-multi", new MemoryStream("msg-0"u8.ToArray()), false, CancellationToken.None);

        // Wait for both consumers to receive msg-0
        for (int i = 0; i < 30 && (c1Messages.Count < 1 || c2Messages.Count < 1); i++)
            await Task.Delay(100);

        Assert.Contains("msg-0", c1Messages);
        Assert.Contains("msg-0", c2Messages);

        // Push second message
        await producer.Queue.Push("ack-push-multi", new MemoryStream("msg-1"u8.ToArray()), false, CancellationToken.None);

        // Neither consumer has acked msg-0 → msg-1 should be blocked
        await Task.Delay(500);
        Assert.DoesNotContain("msg-1", c1Messages);
        Assert.DoesNotContain("msg-1", c2Messages);

        // Consumer 1 acks msg-0
        Assert.NotNull(c1Pending);
        await consumer1.SendAsync(c1Pending.CreateAcknowledge(), CancellationToken.None);

        // After one consumer acks, the next message should be dispatched.
        // The server uses a single TaskCompletionSource — the first ack releases the lock.
        await Task.Delay(500);
        bool msg1DeliveredAfterOneAck = c1Messages.Contains("msg-1") || c2Messages.Contains("msg-1");
        Assert.True(msg1DeliveredAfterOneAck, "Next message should be dispatched after the first consumer acks");

        // Consumer 2 acks msg-0
        Assert.NotNull(c2Pending);
        await consumer2.SendAsync(c2Pending.CreateAcknowledge(), CancellationToken.None);

        // Wait for msg-1 to be delivered to both
        for (int i = 0; i < 50 && (!c1Messages.Contains("msg-1") || !c2Messages.Contains("msg-1")); i++)
            await Task.Delay(100);

        // Both consumers should have received both messages
        Assert.Contains("msg-1", c1Messages);
        Assert.Contains("msg-1", c2Messages);

        producer.Disconnect();
        consumer1.Disconnect();
        consumer2.Disconnect();
    }
}
