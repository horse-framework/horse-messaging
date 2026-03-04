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

public class PushDeliveryTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_SingleConsumer_ReceivesMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-single", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-single", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-single", new MemoryStream("hello"u8.ToArray()), true);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        Assert.Equal("hello", received.ToString());

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_MultipleConsumers_AllReceive(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-multi", o => o.Type = QueueType.Push);

        int count1 = 0, count2 = 0, count3 = 0;

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c1.Queue.Subscribe("push-multi", true);
        c1.MessageReceived += (_, _) => count1++;

        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.Queue.Subscribe("push-multi", true);
        c2.MessageReceived += (_, _) => count2++;

        HorseClient c3 = new HorseClient();
        await c3.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c3.Queue.Subscribe("push-multi", true);
        c3.MessageReceived += (_, _) => count3++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-multi", new MemoryStream("broadcast"u8.ToArray()), true);

        for (int i = 0; i < 30 && (count1 == 0 || count2 == 0 || count3 == 0); i++)
            await Task.Delay(100);

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
        Assert.Equal(1, count3);

        producer.Disconnect();
        c1.Disconnect();
        c2.Disconnect();
        c3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_NoConsumer_MessageStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-store", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-store", new MemoryStream("stored"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("push-store");
        Assert.NotNull(queue);
        Assert.False(queue.IsEmpty);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_ContentPreserved_RoundTrip(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-content", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-content", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        string longContent = new string('X', 5000);
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-content", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(longContent)), true);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        Assert.Equal(longContent, received.ToString());

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_MultipleMessages_OrderPreserved(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-order", o => o.Type = QueueType.Push);

        List<string> receivedMessages = new();
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-order", true);
        consumer.MessageReceived += (_, m) => { lock (receivedMessages) receivedMessages.Add(m.ToString()); };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 10; i++)
            await producer.Queue.Push("push-order", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);

        for (int i = 0; i < 50 && receivedMessages.Count < 10; i++)
            await Task.Delay(100);

        Assert.Equal(10, receivedMessages.Count);
        for (int i = 0; i < 10; i++)
            Assert.Equal($"msg-{i}", receivedMessages[i]);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_WithHeaders_HeadersPreserved(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-hdr", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-hdr", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        var headers = new[]
        {
            new KeyValuePair<string, string>("X-Tenant", "acme"),
            new KeyValuePair<string, string>("X-Priority", "high")
        };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-hdr", new MemoryStream("with headers"u8.ToArray()), true, headers);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        Assert.Equal("acme", received.FindHeader("X-Tenant"));
        Assert.Equal("high", received.FindHeader("X-Priority"));

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_HighPriority_DeliveredFirst(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-pri", o => o.Type = QueueType.Push);

        // Push messages without consumer → they queue up
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Regular messages
        for (int i = 0; i < 3; i++)
        {
            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "push-pri");
            msg.SetStringContent($"regular-{i}");
            await producer.SendAsync(msg, CancellationToken.None);
        }

        // High priority message
        HorseMessage hiMsg = new HorseMessage(MessageType.QueueMessage, "push-pri");
        hiMsg.HighPriority = true;
        hiMsg.SetStringContent("priority-first");
        await producer.SendAsync(hiMsg, CancellationToken.None);

        await Task.Delay(500);

        // Now subscribe → first message should be the priority one
        List<string> received = new();
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m.ToString()); };
        await consumer.Queue.Subscribe("push-pri", true);

        for (int i = 0; i < 30 && received.Count < 4; i++)
            await Task.Delay(100);

        Assert.Equal(4, received.Count);
        Assert.Equal("priority-first", received[0]);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_StoredMessages_DeliveredWhenConsumerJoins(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-delayed-sub", o => o.Type = QueueType.Push);

        // Push messages before any consumer exists
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("push-delayed-sub", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"stored-{i}")), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("push-delayed-sub");
        Assert.False(queue.IsEmpty);

        // Now a consumer subscribes → should receive stored messages
        List<string> received = new();
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) => { lock (received) received.Add(m.ToString()); };
        await consumer.Queue.Subscribe("push-delayed-sub", true);

        for (int i = 0; i < 50 && received.Count < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, received.Count);
        Assert.Equal("stored-0", received[0]);
        Assert.Equal("stored-1", received[1]);
        Assert.Equal("stored-2", received[2]);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_StatusPaused_Rejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseQueue queue = await ctx.Rider.Queue.Create("push-paused", o => o.Type = QueueType.Push);
        queue.SetStatus(QueueStatus.Paused);

        PushResult result = await queue.Push("rejected");
        Assert.Equal(PushResult.StatusNotSupported, result);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_StatusOnlyConsume_Rejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseQueue queue = await ctx.Rider.Queue.Create("push-oc", o => o.Type = QueueType.Push);
        queue.SetStatus(QueueStatus.OnlyConsume);

        PushResult result = await queue.Push("rejected-oc");
        Assert.Equal(PushResult.StatusNotSupported, result);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_EmptyContent_Delivered(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-empty", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-empty", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-empty", new MemoryStream(System.Array.Empty<byte>()), true);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        Assert.Equal(0UL, received.Length);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_LargePayload_Delivered(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-large", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-large", true);

        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        byte[] largeData = new byte[128 * 1024]; // 128KB
        System.Random.Shared.NextBytes(largeData);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-large", new MemoryStream(largeData), true);

        for (int i = 0; i < 50 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        Assert.Equal(largeData.Length, (int)received.Length);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Push_ConsumerDisconnects_NextMessageStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("push-disc", o => o.Type = QueueType.Push);

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("push-disc", true);

        // First message delivered
        HorseMessage received = null;
        consumer.MessageReceived += (_, m) => received = m;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("push-disc", new MemoryStream("msg1"u8.ToArray()), true);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);
        Assert.NotNull(received);

        // Consumer disconnects
        consumer.Disconnect();
        await Task.Delay(500);

        // Second message should be stored
        await producer.Queue.Push("push-disc", new MemoryStream("msg2"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("push-disc");
        Assert.False(queue.IsEmpty);

        producer.Disconnect();
    }
}
