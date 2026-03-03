using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues.Core;

public class QueueDestroyTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_Disabled_QueueSurvives(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-dis", o =>
        {
            o.Type = QueueType.Push;
            o.AutoDestroy = QueueDestroy.Disabled;
        });

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        await client.Queue.Subscribe("dest-dis", true);
        await client.Queue.Unsubscribe("dest-dis", true);
        await Task.Delay(2000);

        Assert.NotNull(ctx.Rider.Queue.Find("dest-dis"));
        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_NoConsumers_QueueDestroyedWhenLastLeaves(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-nc", o =>
        {
            o.Type = QueueType.Push;
            o.AutoDestroy = QueueDestroy.NoConsumers;
        });

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        await client.Queue.Subscribe("dest-nc", true);
        await Task.Delay(200);

        Assert.NotNull(ctx.Rider.Queue.Find("dest-nc"));

        await client.Queue.Unsubscribe("dest-nc", true);

        // Wait for auto-destroy check (timer runs every 5s)
        for (int i = 0; i < 40 && ctx.Rider.Queue.Find("dest-nc") != null; i++)
            await Task.Delay(250);

        Assert.Null(ctx.Rider.Queue.Find("dest-nc"));
        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_NoMessages_QueueDestroyedWhenEmpty(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("dest-nm", o =>
        {
            o.Type = QueueType.Push;
            o.AutoDestroy = QueueDestroy.NoMessages;
        });

        HorseQueue queue = ctx.Rider.Queue.Find("dest-nm");
        Assert.NotNull(queue);

        // Queue has no messages and NoMessages policy → should destroy
        for (int i = 0; i < 40 && ctx.Rider.Queue.Find("dest-nm") != null; i++)
            await Task.Delay(250);

        Assert.Null(ctx.Rider.Queue.Find("dest-nm"));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_Empty_BothConditionsMet(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-empty", o =>
        {
            o.Type = QueueType.Push;
            o.AutoDestroy = QueueDestroy.Empty;
        });

        // No consumers + no messages → should destroy
        for (int i = 0; i < 40 && ctx.Rider.Queue.Find("dest-empty") != null; i++)
            await Task.Delay(250);

        Assert.Null(ctx.Rider.Queue.Find("dest-empty"));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_Explicit_ViaRider_Remove(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseQueue queue = await ctx.Rider.Queue.Create("dest-explicit", o => o.Type = QueueType.Push);
        Assert.NotNull(queue);

        await ctx.Rider.Queue.Remove(queue);
        await Task.Delay(200);

        Assert.Null(ctx.Rider.Queue.Find("dest-explicit"));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_WithPendingMessages_MessagesLost(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.None;
            o.Acknowledge = QueueAckDecision.None;
        });

        HorseQueue queue = await ctx.Rider.Queue.Create("dest-pending", o =>
        {
            o.Type = QueueType.Push;
            o.AutoDestroy = QueueDestroy.Disabled;
        });

        // Push message without subscriber so it stays in store
        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        await client.Queue.Push("dest-pending", new MemoryStream("msg"u8.ToArray()), false);
        await Task.Delay(500);

        Assert.False(queue.IsEmpty);

        await ctx.Rider.Queue.Remove(queue);
        Assert.True(queue.IsDestroyed);

        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_ThenFind_ReturnsNull(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-find", o => o.Type = QueueType.Push);
        HorseQueue queue = ctx.Rider.Queue.Find("dest-find");
        Assert.NotNull(queue);

        await ctx.Rider.Queue.Remove(queue);

        HorseQueue afterDestroy = ctx.Rider.Queue.Find("dest-find");
        Assert.Null(afterDestroy);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_ThenRecreate_Succeeds(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-recreate", o => o.Type = QueueType.Push);
        HorseQueue queue = ctx.Rider.Queue.Find("dest-recreate");
        await ctx.Rider.Queue.Remove(queue);
        Assert.Null(ctx.Rider.Queue.Find("dest-recreate"));

        await ctx.Rider.Queue.Create("dest-recreate", o => o.Type = QueueType.RoundRobin);
        HorseQueue recreated = ctx.Rider.Queue.Find("dest-recreate");
        Assert.NotNull(recreated);
        Assert.Equal(QueueType.RoundRobin, recreated.Type);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_Empty_WithConsumer_SurvivesUntilConsumerLeaves(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-empty-sub", o =>
        {
            o.Type = QueueType.Push;
            o.AutoDestroy = QueueDestroy.Empty;
        });

        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        await client.Queue.Subscribe("dest-empty-sub", true);
        await Task.Delay(500);

        // Queue has no messages but has consumer → should survive with Empty policy
        // (Empty = NoMessages AND NoConsumers)
        for (int i = 0; i < 12; i++)
            await Task.Delay(500);

        Assert.NotNull(ctx.Rider.Queue.Find("dest-empty-sub"));

        // Now unsubscribe → should destroy
        await client.Queue.Unsubscribe("dest-empty-sub", true);

        for (int i = 0; i < 40 && ctx.Rider.Queue.Find("dest-empty-sub") != null; i++)
            await Task.Delay(250);

        Assert.Null(ctx.Rider.Queue.Find("dest-empty-sub"));
        client.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_IsDestroyed_Flag_Set(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        HorseQueue queue = await ctx.Rider.Queue.Create("dest-flag", o => o.Type = QueueType.Push);
        Assert.False(queue.IsDestroyed);

        await ctx.Rider.Queue.Remove(queue);
        Assert.True(queue.IsDestroyed);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task Destroy_RemoveByName_Works(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("dest-byname", o => o.Type = QueueType.Push);
        Assert.NotNull(ctx.Rider.Queue.Find("dest-byname"));

        await ctx.Rider.Queue.Remove("dest-byname");
        Assert.Null(ctx.Rider.Queue.Find("dest-byname"));
    }
}
