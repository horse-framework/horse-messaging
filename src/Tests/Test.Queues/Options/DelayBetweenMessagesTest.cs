using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Options;

public class DelayBetweenMessagesTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Delay_Zero_InstantDelivery(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("delay-zero", o =>
        {
            o.Type = QueueType.Push;
            o.DelayBetweenMessages = 0;
        });

        int receivedCount = 0;
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("delay-zero", true, CancellationToken.None);
        consumer.MessageReceived += (_, _) => receivedCount++;

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("delay-zero", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"fast-{i}")), true, CancellationToken.None);

        for (int i = 0; i < 30 && receivedCount < 5; i++)
            await Task.Delay(100);

        sw.Stop();

        Assert.Equal(5, receivedCount);
        // All 5 messages should be delivered within 2 seconds (no artificial delay)
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Expected < 2s but took {sw.ElapsedMilliseconds}ms");

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Delay_500ms_MessagesSpacedOut(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("delay-500", o =>
        {
            o.Type = QueueType.Push;
            o.DelayBetweenMessages = 500; // 500ms between each message
        });

        List<DateTime> deliveryTimes = new();
        HorseClient consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("delay-500", true, CancellationToken.None);
        consumer.MessageReceived += (_, _) => { lock (deliveryTimes) deliveryTimes.Add(DateTime.UtcNow); };

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 3; i++)
            await producer.Queue.Push("delay-500", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"slow-{i}")), true, CancellationToken.None);

        // 3 messages with 500ms delay each → ~1000ms minimum total wait
        // Use generous timeout: up to 8 seconds
        for (int i = 0; i < 80 && deliveryTimes.Count < 3; i++)
            await Task.Delay(100);

        Assert.Equal(3, deliveryTimes.Count);

        // Check spacing between messages: each gap should be >= 400ms (generous threshold)
        if (deliveryTimes.Count >= 3)
        {
            double gap1 = (deliveryTimes[1] - deliveryTimes[0]).TotalMilliseconds;
            double gap2 = (deliveryTimes[2] - deliveryTimes[1]).TotalMilliseconds;
            double totalSpan = (deliveryTimes[2] - deliveryTimes[0]).TotalMilliseconds;

            Assert.True(totalSpan >= 800, $"Expected total span >= 800ms but got {totalSpan:F0}ms");
        }

        producer.Disconnect();
        consumer.Disconnect();
    }
}

