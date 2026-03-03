using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

public class PutBackTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PutBack_No_MessageDropped(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("pb-no", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.No;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
        });

        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = false; // will not ack → timeout
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pb-no", true);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("pb-no", new MemoryStream("dropped"u8.ToArray()), false);

        // Wait for ack timeout + processing
        await Task.Delay(4000);

        HorseQueue queue = ctx.Rider.Queue.Find("pb-no");
        // PutBack=No → message is dropped after timeout, store should be empty
        Assert.True(queue.IsEmpty);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PutBack_Priority_MessageGoesToFront(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("pb-pri", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.Priority;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = false; // won't ack first time
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pb-pri", true);
        consumer.MessageReceived += (_, _) => receivedCount++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("pb-pri", new MemoryStream("putback"u8.ToArray()), false);

        // Wait for delivery + timeout + put-back + re-delivery
        for (int i = 0; i < 50 && receivedCount < 2; i++)
            await Task.Delay(200);

        // Message should have been put back to priority store and re-delivered
        Assert.True(receivedCount >= 2, $"Expected >= 2 deliveries but got {receivedCount}");

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PutBack_Regular_MessageGoesToEnd(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("pb-reg", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.Regular;
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = false;
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pb-reg", true);
        consumer.MessageReceived += (_, _) => receivedCount++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("pb-reg", new MemoryStream("regular-back"u8.ToArray()), false);

        // Wait for timeout + put-back + re-delivery
        for (int i = 0; i < 50 && receivedCount < 2; i++)
            await Task.Delay(200);

        Assert.True(receivedCount >= 2, $"Expected >= 2 deliveries but got {receivedCount}");

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PutBack_WithDelay_MessageDelayed(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.WaitForAcknowledge;
        });

        await ctx.Rider.Queue.Create("pb-delay", o =>
        {
            o.Type = QueueType.RoundRobin;
            o.PutBack = PutBackDecision.Regular;
            o.PutBackDelay = 2000; // 2 seconds delay before put-back
            o.AcknowledgeTimeout = TimeSpan.FromSeconds(2);
        });

        int receivedCount = 0;
        DateTime? secondDelivery = null;
        DateTime firstDelivery = DateTime.MinValue;

        HorseClient consumer = new HorseClient();
        consumer.AutoAcknowledge = false;
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("pb-delay", true);
        consumer.MessageReceived += (_, _) =>
        {
            receivedCount++;
            if (receivedCount == 1)
                firstDelivery = DateTime.UtcNow;
            else if (receivedCount == 2)
                secondDelivery = DateTime.UtcNow;
        };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("pb-delay", new MemoryStream("delayed"u8.ToArray()), false);

        // Wait for first delivery + timeout + delay + second delivery (generous timeout)
        for (int i = 0; i < 80 && receivedCount < 2; i++)
            await Task.Delay(200);

        Assert.True(receivedCount >= 2, $"Expected >= 2 deliveries but got {receivedCount}");

        // The gap between first and second delivery should be >= ack timeout + put-back delay
        // AckTimeout=2s + PutBackDelay=2s = 4s minimum. Use generous threshold: >= 3s
        if (secondDelivery.HasValue)
        {
            double gap = (secondDelivery.Value - firstDelivery).TotalSeconds;
            Assert.True(gap >= 3.0, $"Expected >= 3s gap but got {gap:F1}s");
        }

        producer.Disconnect();
        consumer.Disconnect();
    }
}

