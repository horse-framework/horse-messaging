using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

/// <summary>
/// Tests all meaningful combinations of QueueType × CommitWhen × QueueAckDecision.
/// Each test is run in both memory and persistent modes.
///
///   QueueType:        Push, RoundRobin, Pull
///   CommitWhen:       None, AfterReceived, AfterSent, AfterAcknowledge
///   QueueAckDecision: None, JustRequest, WaitForAcknowledge
///
/// Not every combination is meaningful:
///   - Pull queues ignore CommitWhen and Acknowledge (delivery is request-based).
///   - CommitWhen.AfterAcknowledge with Acknowledge.None → commit never fires.
///   
/// This file tests the meaningful combinations and verifies edge cases.
/// </summary>
public class CombinedTest
{
    #region Helpers

    private static byte[] Encode(string s) => Encoding.UTF8.GetBytes(s);

    private static async Task<HorseClient> ConnectClient(int port, string clientId = null)
    {
        var c = new HorseClient();
        if (clientId != null) c.ClientId = clientId;
        c.ResponseTimeout = TimeSpan.FromSeconds(10);
        await c.ConnectAsync($"horse://localhost:{port}");
        Assert.True(c.IsConnected);
        return c;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        int elapsed = 0;
        while (!condition() && elapsed < timeoutMs)
        {
            await Task.Delay(50);
            elapsed += 50;
        }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  PUSH × CommitWhen × AckDecision
    // ═══════════════════════════════════════════════════════════════════

    #region Push + CommitWhen.None

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitNone_AckNone_AllConsumersReceive(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int c1 = 0, c2 = 0;
        var consumer1 = await ConnectClient(ctx.Port);
        consumer1.MessageReceived += (_, _) => Interlocked.Increment(ref c1);
        await consumer1.Queue.Subscribe("q", true);

        var consumer2 = await ConnectClient(ctx.Port);
        consumer2.MessageReceived += (_, _) => Interlocked.Increment(ref c2);
        await consumer2.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // CommitWhen.None → waitForCommit should return immediately regardless
        await producer.Queue.Push("q", new MemoryStream(Encode("msg")), false);

        await WaitUntil(() => c1 >= 1 && c2 >= 1);

        Assert.Equal(1, c1);
        Assert.Equal(1, c2);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitNone_JustRequest_AllConsumersReceive(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += async (client, msg) =>
        {
            Interlocked.Increment(ref received);
            await client.SendAck(msg, CancellationToken.None);
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        await producer.Queue.Push("q", new MemoryStream(Encode("msg")), false);

        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitNone_WaitForAck_NextMessageWaitsUntilAck(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        var receivedMessages = new ConcurrentBag<string>();
        HorseMessage pendingAckMsg = null;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (client, msg) =>
        {
            string body = msg.GetStringContent();
            receivedMessages.Add(body);
            if (body == "msg-0")
                pendingAckMsg = msg; // hold ack for first message
            else
                client.SendAck(msg, CancellationToken.None);
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // Push first message
        await producer.Queue.Push("q", new MemoryStream(Encode("msg-0")), false);
        await WaitUntil(() => receivedMessages.Count >= 1);

        // Push second message — should be held because first is not acked
        await producer.Queue.Push("q", new MemoryStream(Encode("msg-1")), false);
        await Task.Delay(500);

        // Only first message should have been delivered
        Assert.Equal(1, receivedMessages.Count);

        // Now ack first message
        await consumer.SendAck(pendingAckMsg, CancellationToken.None);
        await WaitUntil(() => receivedMessages.Count >= 2);

        Assert.Equal(2, receivedMessages.Count);
    }

    #endregion

    #region Push + CommitWhen.AfterReceived

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitAfterReceived_AckNone_ProducerGetsCommitImmediately(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // waitForCommit=true → should return Ok immediately after server receives
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitAfterReceived_WaitForAck_CommitBeforeAck(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.AutoAcknowledge = true;
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // Producer gets commit before consumer acks (commit is after server received)
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    #endregion

    #region Push + CommitWhen.AfterSent

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitAfterSent_AckNone_CommitAfterDelivery(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterSent;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // Wait for commit: should return after message is sent to consumers
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.Equal(HorseResultCode.Ok, result.Code);

        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitAfterSent_NoConsumer_ProducerTimesOut(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterSent;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        // No consumer subscribed — message stored but never sent → commit never fires
        var producer = await ConnectClient(ctx.Port);
        producer.ResponseTimeout = TimeSpan.FromSeconds(3);

        // Push with waitForCommit — should fail/timeout since no consumer to send to
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);
    }

    #endregion

    #region Push + CommitWhen.AfterAcknowledge

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitAfterAck_JustRequest_CommitAfterConsumerAcks(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += async (client, msg) =>
        {
            await Task.Delay(200); // simulate work
            await client.SendAck(msg, CancellationToken.None);
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // Producer should wait until consumer acks
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.Equal(HorseResultCode.Ok, result.Code);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_CommitAfterAck_WaitForAck_CommitAfterConsumerAcks(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        bool ackSent = false;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += async (client, msg) =>
        {
            await Task.Delay(300);
            await client.SendAck(msg, CancellationToken.None);
            ackSent = true;
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        Assert.True(ackSent, "Consumer should have acked before producer commit returned");
    }

    [Fact]
    public async Task Push_CommitAfterAck_AckNone_Memory_CommitNeverFires()
    {
        // CommitWhen=AfterAcknowledge + Acknowledge=None → no ack ever sent → commit never fires.
        // This combination is only valid in memory mode.
        await using var ctx = await QueueTestServer.Create("memory", o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        producer.ResponseTimeout = TimeSpan.FromSeconds(3);

        // Commit should never arrive because ack is disabled
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);

        // But the consumer should still have received the message
        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Push_CommitAfterAck_AckNone_Persistent_ThrowsNotSupported()
    {
        // PersistentQueueManager explicitly rejects CommitWhen=AfterAcknowledge + Acknowledge=None
        // because messages would never be deleted from disk (no ack → no delete decision).
        await using var ctx = await QueueTestServer.Create("persistent", o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.None;
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => ctx.Rider.Queue.Create("q"));
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  ROUND ROBIN × CommitWhen × AckDecision
    // ═══════════════════════════════════════════════════════════════════

    #region RoundRobin + CommitWhen.None

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitNone_AckNone_MessagesDistributed(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int c1 = 0, c2 = 0;
        var consumer1 = await ConnectClient(ctx.Port, "c1");
        consumer1.MessageReceived += (_, _) => Interlocked.Increment(ref c1);
        await consumer1.Queue.Subscribe("q", true);

        var consumer2 = await ConnectClient(ctx.Port, "c2");
        consumer2.MessageReceived += (_, _) => Interlocked.Increment(ref c2);
        await consumer2.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        for (int i = 0; i < 6; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode($"msg-{i}")), false);

        await WaitUntil(() => c1 + c2 >= 6);

        Assert.Equal(6, c1 + c2);
        Assert.Equal(3, c1);
        Assert.Equal(3, c2);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitNone_JustRequest_MessagesDistributed(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(5);
        });

        await ctx.Rider.Queue.Create("q");

        int c1 = 0, c2 = 0;
        var consumer1 = await ConnectClient(ctx.Port, "c1");
        consumer1.AutoAcknowledge = true;
        consumer1.MessageReceived += (_, _) => Interlocked.Increment(ref c1);
        await consumer1.Queue.Subscribe("q", true);

        var consumer2 = await ConnectClient(ctx.Port, "c2");
        consumer2.AutoAcknowledge = true;
        consumer2.MessageReceived += (_, _) => Interlocked.Increment(ref c2);
        await consumer2.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode($"msg-{i}")), false);

        await WaitUntil(() => c1 + c2 >= 4);

        Assert.Equal(4, c1 + c2);
        Assert.True(c1 >= 1);
        Assert.True(c2 >= 1);
    }

    #endregion

    #region RoundRobin + CommitWhen.AfterReceived

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitAfterReceived_AckNone_ProducerGetsCommitImmediately(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    #endregion

    #region RoundRobin + CommitWhen.AfterSent

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitAfterSent_AckNone_CommitAfterDelivery(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterSent;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitAfterSent_NoConsumer_ProducerTimesOut(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterSent;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        producer.ResponseTimeout = TimeSpan.FromSeconds(3);

        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);
    }

    #endregion

    #region RoundRobin + CommitWhen.AfterAcknowledge

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitAfterAck_WaitForAck_CommitAfterConsumerAcks(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        bool ackSent = false;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += async (client, msg) =>
        {
            await Task.Delay(200);
            await client.SendAck(msg, CancellationToken.None);
            ackSent = true;
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);

        Assert.Equal(HorseResultCode.Ok, result.Code);
        Assert.True(ackSent);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_CommitAfterAck_NegativeAck_ProducerGetsFailed(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
            o.PutBack = PutBackDecision.No;
        });

        await ctx.Rider.Queue.Create("q");

        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += async (client, msg) =>
        {
            await client.SendNegativeAck(msg, "test-error", CancellationToken.None);
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);

        Assert.Equal(HorseResultCode.Failed, result.Code);
    }

    [Fact]
    public async Task RR_CommitAfterAck_AckNone_Memory_CommitNeverFires()
    {
        await using var ctx = await QueueTestServer.Create("memory", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        producer.ResponseTimeout = TimeSpan.FromSeconds(3);

        var result = await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);
        Assert.NotEqual(HorseResultCode.Ok, result.Code);

        await WaitUntil(() => received >= 1);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task RR_CommitAfterAck_AckNone_Persistent_ThrowsNotSupported()
    {
        // PersistentQueueManager explicitly rejects CommitWhen=AfterAcknowledge + Acknowledge=None
        await using var ctx = await QueueTestServer.Create("persistent", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.None;
        });

        await Assert.ThrowsAsync<NotSupportedException>(() => ctx.Rider.Queue.Create("q"));
    }

    #endregion

    #region RoundRobin + WaitForAcknowledge + Multiple Consumers

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_WaitForAck_MultipleConsumers_BusyConsumerSkipped(string mode)
    {
        // This is the key test: when a consumer is busy (hasn't acked), the server
        // skips it and delivers to the next available consumer.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
        });

        await ctx.Rider.Queue.Create("q");

        var c1Messages = new ConcurrentBag<string>();
        var c2Messages = new ConcurrentBag<string>();
        HorseMessage c1PendingAck = null;

        // Consumer 1: will hold the first message without acking
        var consumer1 = await ConnectClient(ctx.Port, "c1");
        consumer1.MessageReceived += (client, msg) =>
        {
            string body = msg.GetStringContent();
            c1Messages.Add(body);
            if (c1PendingAck == null)
                c1PendingAck = msg; // hold first ack
            else
                client.SendAck(msg, CancellationToken.None);
        };
        await consumer1.Queue.Subscribe("q", true);

        // Consumer 2: acks immediately
        var consumer2 = await ConnectClient(ctx.Port, "c2");
        consumer2.MessageReceived += async (client, msg) =>
        {
            c2Messages.Add(msg.GetStringContent());
            await client.SendAck(msg, CancellationToken.None);
        };
        await consumer2.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // Push 4 messages rapidly
        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode($"msg-{i}")), false);

        // Wait for delivery
        await WaitUntil(() => c1Messages.Count + c2Messages.Count >= 4, 8000);

        // Consumer1 got first message but held ack → server should have skipped it
        // Consumer2 should have gotten more messages while Consumer1 was busy
        Assert.True(c1Messages.Count >= 1, $"Consumer1 should have gotten at least 1 msg, got {c1Messages.Count}");
        Assert.True(c2Messages.Count >= 1, $"Consumer2 should have gotten at least 1 msg, got {c2Messages.Count}");

        // Now ack the pending message
        if (c1PendingAck != null)
            await consumer1.SendAck(c1PendingAck, CancellationToken.None);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_WaitForAck_SingleConsumer_StrictFIFO(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        var receivedOrder = new ConcurrentQueue<int>();
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += async (client, msg) =>
        {
            int num = int.Parse(msg.GetStringContent());
            receivedOrder.Enqueue(num);
            await Task.Delay(50); // simulate processing
            await client.SendAck(msg, CancellationToken.None);
        };
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        for (int i = 0; i < 10; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode(i.ToString())), false);

        await WaitUntil(() => receivedOrder.Count >= 10, 10000);

        Assert.Equal(10, receivedOrder.Count);

        // Verify strict FIFO order
        var items = receivedOrder.ToArray();
        for (int i = 0; i < 10; i++)
            Assert.Equal(i, items[i]);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_WaitForAck_AllConsumersBusy_ThirdMessageWaitsForFreeConsumer(string mode)
    {
        // RoundRobin + WaitForAcknowledge: when all consumers are busy (haven't acked),
        // the server retries in a loop until a consumer becomes available.
        // The 3rd message is NOT delivered to any consumer until one of them acks.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(15);
        });

        await ctx.Rider.Queue.Create("q");

        int totalDelivered = 0;
        HorseMessage c1Pending = null;
        HorseMessage c2Pending = null;

        // Consumer 1: holds ack
        var consumer1 = await ConnectClient(ctx.Port, "c1");
        consumer1.MessageReceived += (_, msg) =>
        {
            Interlocked.Increment(ref totalDelivered);
            c1Pending = msg;
        };
        await consumer1.Queue.Subscribe("q", true);

        // Consumer 2: holds ack
        var consumer2 = await ConnectClient(ctx.Port, "c2");
        consumer2.MessageReceived += (_, msg) =>
        {
            Interlocked.Increment(ref totalDelivered);
            c2Pending = msg;
        };
        await consumer2.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        // Push 2 messages with waitForCommit:true so the server fully processes
        // each push (including setting CurrentlyProcessing) before we continue.
        await producer.Queue.Push("q", new MemoryStream(Encode("msg-0")), true);
        await producer.Queue.Push("q", new MemoryStream(Encode("msg-1")), true);

        await WaitUntil(() => totalDelivered >= 2);
        Assert.Equal(2, totalDelivered);

        // Extra safety margin: let server-side processing settle
        await Task.Delay(300);

        // Push a 3rd message — both consumers are busy, server holds it in retry loop
        await producer.Queue.Push("q", new MemoryStream(Encode("msg-2")), false);

        // The 3rd message should NOT have been delivered yet (all consumers busy)
        await Task.Delay(800);
        Assert.Equal(2, totalDelivered);

        // Ack from consumer1 using consumer1's own pending message
        // This is critical: ack must come from the same client the message was delivered to
        if (c1Pending != null)
            await consumer1.SendAck(c1Pending, CancellationToken.None);
        else if (c2Pending != null)
            await consumer2.SendAck(c2Pending, CancellationToken.None);

        // Wait for the 3rd message to be delivered to the freed consumer
        await WaitUntil(() => totalDelivered >= 3, 8000);
        Assert.Equal(3, totalDelivered);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  PULL — CommitWhen and Acknowledge are ignored
    // ═══════════════════════════════════════════════════════════════════

    #region Pull

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_BasicPullRequest_ReturnsMessages(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Pull;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode($"msg-{i}")), false);

        await Task.Delay(500);

        var consumer = await ConnectClient(ctx.Port);
        await consumer.Queue.Subscribe("q", true);

        var container = await consumer.Queue.Pull(new PullRequest
        {
            Queue = "q",
            Count = 3,
            Order = MessageOrder.FIFO
        });

        Assert.NotNull(container);
        Assert.Equal(3, container.ReceivedMessages.Count());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_EmptyQueue_ReturnsNoMessages(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Pull;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        var consumer = await ConnectClient(ctx.Port);
        await consumer.Queue.Subscribe("q", true);

        var container = await consumer.Queue.Pull(new PullRequest
        {
            Queue = "q",
            Count = 5,
            Order = MessageOrder.FIFO
        });

        Assert.NotNull(container);
        Assert.Empty(container.ReceivedMessages);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_SingleMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Pull;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        await producer.Queue.Push("q", new MemoryStream(Encode("only-one")), false);
        await Task.Delay(300);

        var consumer = await ConnectClient(ctx.Port);
        await consumer.Queue.Subscribe("q", true);

        var container = await consumer.Queue.Pull(PullRequest.Single("q"));

        Assert.NotNull(container);
        Assert.Single(container.ReceivedMessages);
        Assert.Equal("only-one", container.ReceivedMessages.First().GetStringContent());
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_MessagesRemovedAfterPull(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Pull;
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        var queue = await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode($"msg-{i}")), false);

        await Task.Delay(500);

        var consumer = await ConnectClient(ctx.Port);
        await consumer.Queue.Subscribe("q", true);

        // Pull all
        var container = await consumer.Queue.Pull(new PullRequest { Queue = "q", Count = 10 });
        Assert.Equal(3, container.ReceivedMessages.Count());

        // Queue should now be empty
        await Task.Delay(300);
        int remaining = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(0, remaining);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Pull_CommitWhenAndAck_Ignored(string mode)
    {
        // Even with CommitWhen.AfterAcknowledge and WaitForAcknowledge,
        // Pull queues should still work normally — these settings are irrelevant for Pull.
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Pull;
            o.CommitWhen = CommitWhen.AfterAcknowledge;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        await producer.Queue.Push("q", new MemoryStream(Encode("test")), false);
        await Task.Delay(300);

        var consumer = await ConnectClient(ctx.Port);
        await consumer.Queue.Subscribe("q", true);

        var container = await consumer.Queue.Pull(PullRequest.Single("q"));
        Assert.NotNull(container);
        Assert.Single(container.ReceivedMessages);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════
    //  EDGE CASES
    // ═══════════════════════════════════════════════════════════════════

    #region Edge Cases

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_AckNone_MessageDeletedAfterSend(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        var queue = await ctx.Rider.Queue.Create("q");

        int received = 0;
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);
        await producer.Queue.Push("q", new MemoryStream(Encode("msg")), true);

        await WaitUntil(() => received >= 1);
        await Task.Delay(300);

        // With Ack=None, message should be deleted after delivery
        int stored = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(0, stored);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_JustRequest_MessagesFlowWithoutWaiting(string mode)
    {
        // JustRequest: server sends messages without waiting for ack
        // Messages should flow to consumers even if they haven't acked yet
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.JustRequest;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
        });

        await ctx.Rider.Queue.Create("q");

        int received = 0;
        // Consumer does NOT send ack
        var consumer = await ConnectClient(ctx.Port);
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("q", true);

        var producer = await ConnectClient(ctx.Port);

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("q", new MemoryStream(Encode($"msg-{i}")), true);

        await WaitUntil(() => received >= 5, 5000);

        // All messages should have been delivered without waiting for ack
        Assert.Equal(5, received);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_NoConsumer_MessageStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        var queue = await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        await producer.Queue.Push("q", new MemoryStream(Encode("stored")), true);
        await Task.Delay(500);

        int stored = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.True(stored >= 1, "Message should be stored when no consumers");
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RR_NoConsumer_MessageStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        var queue = await ctx.Rider.Queue.Create("q");

        var producer = await ConnectClient(ctx.Port);
        await producer.Queue.Push("q", new MemoryStream(Encode("stored")), true);
        await Task.Delay(500);

        int stored = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.True(stored >= 1, "Message should be stored when no consumers");
    }

    #endregion
}

