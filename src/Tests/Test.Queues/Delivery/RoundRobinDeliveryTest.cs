using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

public class RoundRobinDeliveryTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_TwoConsumers_MessagesDistributed(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-dist", o => o.Type = QueueType.RoundRobin);

        int count1 = 0, count2 = 0;

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c1.Queue.Subscribe("rr-dist", true);
        c1.MessageReceived += (_, _) => count1++;

        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.Queue.Subscribe("rr-dist", true);
        c2.MessageReceived += (_, _) => count2++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 10; i++)
            await producer.Queue.Push("rr-dist", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);

        for (int i = 0; i < 50 && count1 + count2 < 10; i++)
            await Task.Delay(100);

        Assert.Equal(10, count1 + count2);
        Assert.True(count1 >= 3, $"Consumer1 got {count1}");
        Assert.True(count2 >= 3, $"Consumer2 got {count2}");

        producer.Disconnect();
        c1.Disconnect();
        c2.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_OneConsumer_GetsAll(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-one", o => o.Type = QueueType.RoundRobin);

        int count = 0;

        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("rr-one", true);
        consumer.MessageReceived += (_, _) => count++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("rr-one", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);

        for (int i = 0; i < 30 && count < 5; i++)
            await Task.Delay(100);

        Assert.Equal(5, count);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_ThreeConsumers_EvenDistribution(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-three", o => o.Type = QueueType.RoundRobin);

        int count1 = 0, count2 = 0, count3 = 0;

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c1.Queue.Subscribe("rr-three", true);
        c1.MessageReceived += (_, _) => count1++;

        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.Queue.Subscribe("rr-three", true);
        c2.MessageReceived += (_, _) => count2++;

        HorseClient c3 = new HorseClient();
        await c3.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c3.Queue.Subscribe("rr-three", true);
        c3.MessageReceived += (_, _) => count3++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 9; i++)
            await producer.Queue.Push("rr-three", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), true);

        for (int i = 0; i < 50 && count1 + count2 + count3 < 9; i++)
            await Task.Delay(100);

        Assert.Equal(9, count1 + count2 + count3);
        Assert.Equal(3, count1);
        Assert.Equal(3, count2);
        Assert.Equal(3, count3);

        producer.Disconnect();
        c1.Disconnect();
        c2.Disconnect();
        c3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_ConsumerLeaves_RemainingGetsAll(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-leave", o => o.Type = QueueType.RoundRobin);

        int count1 = 0, count2 = 0;

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c1.Queue.Subscribe("rr-leave", true);
        c1.MessageReceived += (_, _) => count1++;

        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.Queue.Subscribe("rr-leave", true);
        c2.MessageReceived += (_, _) => count2++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Send 2 messages (one each)
        await producer.Queue.Push("rr-leave", new MemoryStream("m1"u8.ToArray()), true);
        await producer.Queue.Push("rr-leave", new MemoryStream("m2"u8.ToArray()), true);

        for (int i = 0; i < 30 && count1 + count2 < 2; i++)
            await Task.Delay(100);

        Assert.Equal(2, count1 + count2);

        // Consumer2 leaves
        c2.Disconnect();
        await Task.Delay(500);

        int c1Before = count1;

        // Send 3 more → all go to c1
        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("rr-leave", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"after-{i}")), true);

        for (int i = 0; i < 30 && count1 < c1Before + 3; i++)
            await Task.Delay(100);

        Assert.Equal(c1Before + 3, count1);

        producer.Disconnect();
        c1.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_NoConsumer_MessageStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-store", o => o.Type = QueueType.RoundRobin);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("rr-store", new MemoryStream("stored"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("rr-store");
        Assert.False(queue.IsEmpty);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_StoredMessages_DeliveredWhenFirstConsumerJoins(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-stored", o => o.Type = QueueType.RoundRobin);

        // Push 3 messages without any consumer
        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("rr-stored", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        // Consumer subscribes → should get all stored messages (one at a time via RR)
        int received = 0;
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, _) => received++;
        await consumer.Queue.Subscribe("rr-stored", true);

        for (int i = 0; i < 50 && received < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, received);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RoundRobin_NewConsumerJoinsMidStream_GetsNextMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("rr-midjoin", o => o.Type = QueueType.RoundRobin);

        int count1 = 0, count2 = 0;

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c1.Queue.Subscribe("rr-midjoin", true);
        c1.MessageReceived += (_, _) => count1++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Send 2 messages → all go to c1
        for (int i = 0; i < 2; i++)
            await producer.Queue.Push("rr-midjoin", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"batch1-{i}")), true);

        for (int i = 0; i < 30 && count1 < 2; i++)
            await Task.Delay(100);
        Assert.Equal(2, count1);

        // c2 joins mid-stream
        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.Queue.Subscribe("rr-midjoin", true);
        c2.MessageReceived += (_, _) => count2++;
        await Task.Delay(300);

        // Send 4 more → distributed between c1 and c2
        for (int i = 0; i < 4; i++)
            await producer.Queue.Push("rr-midjoin", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"batch2-{i}")), true);

        for (int i = 0; i < 50 && count1 + count2 < 6; i++)
            await Task.Delay(100);

        Assert.Equal(6, count1 + count2);
        Assert.True(count2 >= 1, "New consumer should have received at least 1 message");

        producer.Disconnect();
        c1.Disconnect();
        c2.Disconnect();
    }
}

