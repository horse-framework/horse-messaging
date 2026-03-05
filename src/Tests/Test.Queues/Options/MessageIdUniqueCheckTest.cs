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

public class MessageIdUniqueCheckTest
{
    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task UniqueCheck_Enabled_DuplicateRejected(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("uid-on", o =>
        {
            o.Type = QueueType.Push;
            o.MessageIdUniqueCheck = true;
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        // Push first with explicit message id
        HorseResult r1 = await producer.Queue.Push("uid-on", new MemoryStream("first"u8.ToArray()), "dup-id-001", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        // Push with same message id using waitForCommit=false (to avoid client tracker duplicate)
        // then check server store count to verify rejection
        await producer.Queue.Push("uid-on", new MemoryStream("second"u8.ToArray()), "dup-id-001", false, CancellationToken.None);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("uid-on");
        int storeCount = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(1, storeCount); // duplicate was rejected

        producer.Disconnect();
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task UniqueCheck_Disabled_DuplicateAccepted(string mode)
    {
        await using var ctx = await QueueTestServer.Create(mode, o =>
        {
            o.CommitWhen = CommitWhen.AfterReceived;
            o.Acknowledge = QueueAckDecision.None;
        });

        await ctx.Rider.Queue.Create("uid-off", o =>
        {
            o.Type = QueueType.Push;
            o.MessageIdUniqueCheck = false;
        });

        HorseClient producer = new HorseClient();
        await producer.ConnectAsync($"horse://localhost:{ctx.Port}");

        HorseResult r1 = await producer.Queue.Push("uid-off", new MemoryStream("first"u8.ToArray()), "dup-id-002", true, CancellationToken.None);
        Assert.Equal(HorseResultCode.Ok, r1.Code);

        // Same id but unique check off → use waitForCommit=false to avoid client tracker issue
        await producer.Queue.Push("uid-off", new MemoryStream("second"u8.ToArray()), "dup-id-002", false, CancellationToken.None);
        await Task.Delay(500);

        HorseQueue queue = ctx.Rider.Queue.Find("uid-off");
        int storeCount = queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        Assert.Equal(2, storeCount); // both accepted

        producer.Disconnect();
    }
}
