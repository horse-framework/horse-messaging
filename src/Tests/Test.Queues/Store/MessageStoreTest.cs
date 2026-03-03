using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
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
}

