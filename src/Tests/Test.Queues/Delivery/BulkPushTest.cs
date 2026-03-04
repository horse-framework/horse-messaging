using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Delivery;

public class BulkPushTest
{
    private static byte[] Encode(string s) => Encoding.UTF8.GetBytes(s);

    #region PushBulk (raw MemoryStream overload)

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_AllMessagesDelivered(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-raw");

        var received = new ConcurrentBag<string>();
        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) => received.Add(m.GetStringContent());
        await consumer.Queue.Subscribe("bulk-raw", true);

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var contents = new List<MemoryStream>();
        for (int i = 0; i < 10; i++)
            contents.Add(new MemoryStream(Encode($"msg-{i}")));

        producer.Queue.PushBulk("bulk-raw", contents, false, null);

        for (int i = 0; i < 50 && received.Count < 10; i++)
            await Task.Delay(100);

        Assert.Equal(10, received.Count);

        for (int i = 0; i < 10; i++)
            Assert.Contains($"msg-{i}", received);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_WithCallback_CallbackInvokedPerMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-raw-cb");

        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("bulk-raw-cb", true);
        int consumerReceived = 0;
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref consumerReceived);

        var producer = new HorseClient();
        producer.ResponseTimeout = TimeSpan.FromSeconds(10);
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var contents = new List<MemoryStream>();
        for (int i = 0; i < 5; i++)
            contents.Add(new MemoryStream(Encode($"cb-{i}")));

        int callbackCount = 0;
        var callbackResults = new ConcurrentBag<bool>();

        producer.Queue.PushBulk("bulk-raw-cb", contents, true, (msg, success) =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackResults.Add(success);
        });

        // Wait for callbacks (commit is AfterReceived so should be fast)
        for (int i = 0; i < 50 && callbackCount < 5; i++)
            await Task.Delay(100);

        Assert.Equal(5, callbackCount);

        // All callbacks should report success
        foreach (bool result in callbackResults)
            Assert.True(result);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_EmptyList_DoesNothing(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-empty");

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Should not throw or send anything
        producer.Queue.PushBulk("bulk-empty", new List<MemoryStream>(), false, null);
        await Task.Delay(300);

        var queue = ctx.Rider.Queue.Find("bulk-empty");
        Assert.True(queue.IsEmpty);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_RoundRobin_DistributedToConsumers(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.RoundRobin;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-rr");

        int c1Count = 0, c2Count = 0;

        var consumer1 = new HorseClient();
        consumer1.ClientId = "c1";
        await consumer1.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer1.MessageReceived += (_, _) => Interlocked.Increment(ref c1Count);
        await consumer1.Queue.Subscribe("bulk-rr", true);

        var consumer2 = new HorseClient();
        consumer2.ClientId = "c2";
        await consumer2.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer2.MessageReceived += (_, _) => Interlocked.Increment(ref c2Count);
        await consumer2.Queue.Subscribe("bulk-rr", true);

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var contents = new List<MemoryStream>();
        for (int i = 0; i < 10; i++)
            contents.Add(new MemoryStream(Encode($"rr-{i}")));

        producer.Queue.PushBulk("bulk-rr", contents, false, null);

        for (int i = 0; i < 50 && c1Count + c2Count < 10; i++)
            await Task.Delay(100);

        Assert.Equal(10, c1Count + c2Count);
        // Both consumers should have received some messages
        Assert.True(c1Count > 0, "Consumer 1 should have received at least one message");
        Assert.True(c2Count > 0, "Consumer 2 should have received at least one message");

        producer.Disconnect();
        consumer1.Disconnect();
        consumer2.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_NoConsumer_MessagesStored(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-no-consumer");

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var contents = new List<MemoryStream>();
        for (int i = 0; i < 5; i++)
            contents.Add(new MemoryStream(Encode($"stored-{i}")));

        producer.Queue.PushBulk("bulk-no-consumer", contents, false, null);
        await Task.Delay(500);

        var queue = ctx.Rider.Queue.Find("bulk-no-consumer");
        int count = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(5, count);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_WithHeaders_HeadersIncluded(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-headers");

        HorseMessage received = null;
        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) => received = m;
        await consumer.Queue.Subscribe("bulk-headers", true);

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var headers = new List<KeyValuePair<string, string>>
        {
            new("X-Custom", "test-value")
        };

        var contents = new List<MemoryStream> { new(Encode("with-header")) };
        producer.Queue.PushBulk("bulk-headers", contents, false, null, headers);

        for (int i = 0; i < 30 && received == null; i++)
            await Task.Delay(100);

        Assert.NotNull(received);
        string customHeader = received.FindHeader("X-Custom");
        Assert.Equal("test-value", customHeader);

        producer.Disconnect();
        consumer.Disconnect();
    }

    #endregion

    #region PushBulk<T> (model-based overload)

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Model_AllMessagesDelivered(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-model");

        var received = new ConcurrentBag<string>();
        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) => received.Add(m.GetStringContent());
        await consumer.Queue.Subscribe("bulk-model", true);

        var producer = new HorseClient();
        producer.ResponseTimeout = TimeSpan.FromSeconds(10);
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var items = new List<BulkTestModel>();
        for (int i = 0; i < 10; i++)
            items.Add(new BulkTestModel { Id = i, Name = $"item-{i}" });

        producer.Queue.PushBulk("bulk-model", items, null);

        for (int i = 0; i < 50 && received.Count < 10; i++)
            await Task.Delay(100);

        Assert.Equal(10, received.Count);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Model_WithCallback_CallbackInvokedPerMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-model-cb");

        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await consumer.Queue.Subscribe("bulk-model-cb", true);
        consumer.MessageReceived += (_, _) => { };

        var producer = new HorseClient();
        producer.ResponseTimeout = TimeSpan.FromSeconds(10);
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var items = new List<BulkTestModel>();
        for (int i = 0; i < 5; i++)
            items.Add(new BulkTestModel { Id = i, Name = $"cb-{i}" });

        int callbackCount = 0;
        var callbackResults = new ConcurrentBag<bool>();

        producer.Queue.PushBulk("bulk-model-cb", items, (msg, success) =>
        {
            Interlocked.Increment(ref callbackCount);
            callbackResults.Add(success);
        });

        for (int i = 0; i < 50 && callbackCount < 5; i++)
            await Task.Delay(100);

        Assert.Equal(5, callbackCount);

        foreach (bool result in callbackResults)
            Assert.True(result);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Model_EachMessageHasUniqueId(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-unique-id");

        var messageIds = new ConcurrentBag<string>();
        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, m) => messageIds.Add(m.MessageId);
        await consumer.Queue.Subscribe("bulk-unique-id", true);

        var producer = new HorseClient();
        producer.ResponseTimeout = TimeSpan.FromSeconds(10);
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var items = new List<BulkTestModel>();
        for (int i = 0; i < 5; i++)
            items.Add(new BulkTestModel { Id = i, Name = $"uid-{i}" });

        producer.Queue.PushBulk("bulk-unique-id", items, null);

        for (int i = 0; i < 50 && messageIds.Count < 5; i++)
            await Task.Delay(100);

        Assert.Equal(5, messageIds.Count);

        // All message IDs must be unique
        var uniqueIds = new HashSet<string>(messageIds);
        Assert.Equal(5, uniqueIds.Count);

        producer.Disconnect();
        consumer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task PushBulk_Raw_LargeBatch_AllDelivered(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.Type = QueueType.Push;
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("bulk-large");

        int received = 0;
        var consumer = new HorseClient();
        await consumer.ConnectAsync($"horse://localhost:{ctx.Port}");
        consumer.MessageReceived += (_, _) => Interlocked.Increment(ref received);
        await consumer.Queue.Subscribe("bulk-large", true);

        var producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        var contents = new List<MemoryStream>();
        for (int i = 0; i < 100; i++)
            contents.Add(new MemoryStream(Encode($"large-{i}")));

        producer.Queue.PushBulk("bulk-large", contents, false, null);

        for (int i = 0; i < 100 && received < 100; i++)
            await Task.Delay(100);

        Assert.Equal(100, received);

        producer.Disconnect();
        consumer.Disconnect();
    }

    #endregion

    #region Test Model

    public class BulkTestModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    #endregion
}

