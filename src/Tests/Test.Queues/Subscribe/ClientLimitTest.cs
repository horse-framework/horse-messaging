using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Test.Queues.Core;
using Xunit;

namespace Test.Queues.Subscribe;

public class ClientLimitTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ClientLimit_NotReached_AcceptsNewSubscriber(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("cl-ok", o =>
        {
            o.Type = QueueType.Push;
            o.ClientLimit = 5;
        });

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        HorseClient c3 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c3.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult r1 = await c1.Queue.Subscribe("cl-ok", true);
        HorseResult r2 = await c2.Queue.Subscribe("cl-ok", true);
        HorseResult r3 = await c3.Queue.Subscribe("cl-ok", true);

        Assert.Equal(HorseResultCode.Ok, r1.Code);
        Assert.Equal(HorseResultCode.Ok, r2.Code);
        Assert.Equal(HorseResultCode.Ok, r3.Code);

        c1.Disconnect();
        c2.Disconnect();
        c3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ClientLimit_Reached_RejectsNewSubscriber(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("cl-full", o =>
        {
            o.Type = QueueType.Push;
            o.ClientLimit = 2;
        });

        HorseClient c1 = new HorseClient();
        HorseClient c2 = new HorseClient();
        HorseClient c3 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c3.ConnectAsync($"horse://localhost:{ctx.Port}");

        await c1.Queue.Subscribe("cl-full", true);
        await c2.Queue.Subscribe("cl-full", true);

        // Third client exceeds limit
        HorseResult r3 = await c3.Queue.Subscribe("cl-full", true);
        Assert.Equal(HorseResultCode.LimitExceeded, r3.Code);

        c1.Disconnect();
        c2.Disconnect();
        c3.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ClientLimit_Zero_Unlimited(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("cl-unlim", o =>
        {
            o.Type = QueueType.Push;
            o.ClientLimit = 0; // unlimited
        });

        HorseClient[] clients = new HorseClient[10];
        for (int i = 0; i < 10; i++)
        {
            clients[i] = new HorseClient();
            await clients[i].ConnectAsync($"horse://localhost:{ctx.Port}");
            HorseResult r = await clients[i].Queue.Subscribe("cl-unlim", true);
            Assert.Equal(HorseResultCode.Ok, r.Code);
        }

        HorseQueue queue = ctx.Rider.Queue.Find("cl-unlim");
        Assert.Equal(10, queue.Clients.Count());

        foreach (var c in clients)
            c.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task ClientLimit_AfterOneLeaves_NewOneAccepted(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode);

        await ctx.Rider.Queue.Create("cl-leave", o =>
        {
            o.Type = QueueType.Push;
            o.ClientLimit = 2;
        });

        HorseClient c1 = new HorseClient();
        await c1.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c1.Queue.Subscribe("cl-leave", true);

        HorseClient c2 = new HorseClient();
        await c2.ConnectAsync($"horse://localhost:{ctx.Port}");
        await c2.Queue.Subscribe("cl-leave", true);

        // Third client should be rejected
        HorseClient c3 = new HorseClient();
        await c3.ConnectAsync($"horse://localhost:{ctx.Port}");
        HorseResult r3 = await c3.Queue.Subscribe("cl-leave", true);
        Assert.NotEqual(HorseResultCode.Ok, r3.Code);

        // c1 leaves
        await c1.Queue.Unsubscribe("cl-leave", true);
        await Task.Delay(300);

        // Now c3 should be accepted
        HorseResult r3Again = await c3.Queue.Subscribe("cl-leave", true);
        Assert.Equal(HorseResultCode.Ok, r3Again.Code);

        HorseQueue queue = ctx.Rider.Queue.Find("cl-leave");
        Assert.Equal(2, queue.Clients.Count());

        c1.Disconnect();
        c2.Disconnect();
        c3.Disconnect();
    }
}

