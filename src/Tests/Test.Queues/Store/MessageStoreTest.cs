using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Store;

public class MessageStoreTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_PutAndRead_PreservesContent(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-read", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("store-read", new MemoryStream("hello-store"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-read");
        var first = queue.Manager.MessageStore.ReadFirst();
        Assert.NotNull(first);
        Assert.Equal("hello-store", first.Message.ToString());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_ConsumeFirst_RemovesFromStore(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-consume", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("store-consume", new MemoryStream("consumed"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-consume");
        Assert.Equal(1, queue.Manager.MessageStore.Count());

        var consumed = queue.Manager.MessageStore.ConsumeFirst();
        Assert.NotNull(consumed);
        Assert.Equal(0, queue.Manager.MessageStore.Count());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_Count_ReflectsOperations(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-count", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("store-count", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-count");
        Assert.Equal(5, queue.Manager.MessageStore.Count());

        // Consume one
        queue.Manager.MessageStore.ConsumeFirst();
        Assert.Equal(4, queue.Manager.MessageStore.Count());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_Clear_EmptiesAll(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-clear", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 10; i++)
            await producer.Queue.Push("store-clear", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-clear");
        Assert.Equal(10, queue.Manager.MessageStore.Count());

        // Clear via client API
        HorseClient admin = new HorseClient();
        await admin.ConnectAsync($"horse://localhost:{ctx.Port}");
        await admin.Queue.ClearMessages("store-clear", true, true);
        await Task.Delay(500);

        Assert.Equal(0, queue.Manager.MessageStore.Count());

        producer.Disconnect();
        admin.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_FindById_ReturnsCorrect(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-find", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push with explicit message id
        await producer.Queue.Push("store-find", new MemoryStream("findme"u8.ToArray()), "find-id-001", false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-find");
        var found = queue.Manager.MessageStore.Find("find-id-001");
        Assert.NotNull(found);
        Assert.Equal("findme", found.Message.ToString());

        // Non-existent id
        var notFound = queue.Manager.MessageStore.Find("does-not-exist");
        Assert.Null(notFound);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_ConsumeLast_ReturnsLastMessage(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-last", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        await producer.Queue.Push("store-last", new MemoryStream("first"u8.ToArray()), false);
        await producer.Queue.Push("store-last", new MemoryStream("second"u8.ToArray()), false);
        await producer.Queue.Push("store-last", new MemoryStream("third"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-last");
        var last = queue.Manager.MessageStore.ConsumeLast();
        Assert.NotNull(last);
        Assert.Equal("third", last.Message.ToString());
        Assert.Equal(2, queue.Manager.MessageStore.Count());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_ConsumeMultiple_ReturnsRequestedCount(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-multi", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        for (int i = 0; i < 5; i++)
            await producer.Queue.Push("store-multi", new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"msg-{i}")), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-multi");
        var consumed = queue.Manager.MessageStore.ConsumeMultiple(3);
        Assert.Equal(3, consumed.Count);
        Assert.Equal("msg-0", consumed[0].Message.ToString());
        Assert.Equal("msg-1", consumed[1].Message.ToString());
        Assert.Equal("msg-2", consumed[2].Message.ToString());
        Assert.Equal(2, queue.Manager.MessageStore.Count());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_ConsumeMultiple_MoreThanAvailable_ReturnsAll(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-multi-over", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        await producer.Queue.Push("store-multi-over", new MemoryStream("only-one"u8.ToArray()), false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-multi-over");
        var consumed = queue.Manager.MessageStore.ConsumeMultiple(10);
        Assert.Single(consumed);
        Assert.Equal(0, queue.Manager.MessageStore.Count());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_Remove_ByMessageId(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-remove", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        await producer.Queue.Push("store-remove", new MemoryStream("removeme"u8.ToArray()), "rm-id-001", false);
        await producer.Queue.Push("store-remove", new MemoryStream("keepme"u8.ToArray()), "rm-id-002", false);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-remove");
        Assert.Equal(2, queue.Manager.MessageStore.Count());

        bool removed = queue.Manager.MessageStore.Remove("rm-id-001");
        Assert.True(removed);
        Assert.Equal(1, queue.Manager.MessageStore.Count());

        var remaining = queue.Manager.MessageStore.ReadFirst();
        Assert.Equal("keepme", remaining.Message.ToString());

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_Remove_NonExistent_ReturnsFalse(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-rm-nx", o => o.Type = QueueType.Push);

        HorseQueue queue = ctx.Rider.Queue.Find("store-rm-nx");
        bool removed = queue.Manager.MessageStore.Remove("nonexistent-id");
        Assert.False(removed);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_IsEmpty_ReflectsState(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-empty", o => o.Type = QueueType.Push);

        HorseQueue queue = ctx.Rider.Queue.Find("store-empty");
        Assert.True(queue.Manager.MessageStore.IsEmpty);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");
        await producer.Queue.Push("store-empty", new MemoryStream("data"u8.ToArray()), false);
        await Task.Delay(500);

        Assert.False(queue.Manager.MessageStore.IsEmpty);

        queue.Manager.MessageStore.ConsumeFirst();
        Assert.True(queue.Manager.MessageStore.IsEmpty);

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Store_PriorityStore_SeparateFromMainStore(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("store-pri", o => o.Type = QueueType.Push);

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Normal message
        await producer.Queue.Push("store-pri", new MemoryStream("normal"u8.ToArray()), false);

        // High priority message
        HorseMessage hiMsg = new HorseMessage(MessageType.QueueMessage, "store-pri");
        hiMsg.HighPriority = true;
        hiMsg.SetStringContent("priority");
        await producer.SendAsync(hiMsg);

        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("store-pri");
        int mainCount = queue.Manager.MessageStore.Count();
        int priorityCount = queue.Manager.PriorityMessageStore.Count();

        Assert.Equal(1, mainCount);
        Assert.Equal(1, priorityCount);

        producer.Disconnect();
    }
}

