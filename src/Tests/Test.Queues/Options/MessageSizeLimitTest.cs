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

public class MessageSizeLimitTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SizeLimit_Under_Accepted(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("sz-under", o =>
            {
                o.Type = QueueType.Push;
                o.MessageSizeLimit = 1000; // 1000 bytes limit
            });

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            // 50 bytes — well under limit
            byte[] data = new byte[50];
            HorseResult result = await producer.Queue.Push("sz-under", new MemoryStream(data), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SizeLimit_Over_Rejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("sz-over", o =>
            {
                o.Type = QueueType.Push;
                o.MessageSizeLimit = 100; // 100 bytes limit
            });

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            // 200 bytes — over the limit
            byte[] data = new byte[200];
            HorseResult result = await producer.Queue.Push("sz-over", new MemoryStream(data), true, CancellationToken.None);
            Assert.NotEqual(HorseResultCode.Ok, result.Code);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task SizeLimit_Zero_Unlimited(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });
        try
        {

            await ctx.Rider.Queue.Create("sz-unlim", o =>
            {
                o.Type = QueueType.Push;
                o.MessageSizeLimit = 0; // unlimited
            });

            HorseClient producer = new HorseClient();
            await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

            // 100KB — should be accepted with no limit
            byte[] data = new byte[100 * 1024];
            HorseResult result = await producer.Queue.Push("sz-unlim", new MemoryStream(data), true, CancellationToken.None);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            producer.Disconnect();
        }
        finally
        {
            await ctx.Server.StopAsync();
        }
    }
}

