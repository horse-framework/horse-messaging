using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Xunit;

namespace Test.Queues.Options;

/// <summary>
/// Message limit: concurrent pushes must not overfill the queue, DeleteOldestMessage must cover
/// priority messages too, and a rejected push must not cost the queue an accepted message.
/// </summary>
public class MessageLimitConsistencyTest : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "mlc-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_path, true);
        }
        catch
        {
        }
    }

    private HorseRider CreateRider(string mode)
    {
        Directory.CreateDirectory(_path);
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = _path)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.Acknowledge = QueueAckDecision.None;
                // Persistent queues save the message before adding it to the store: the widest window
                q.Options.CommitWhen = CommitWhen.AfterReceived;

                if (mode == "persistent")
                    q.UsePersistentQueues(db => db.SetPhysicalPath(queue => Path.Combine(_path, $"{queue.Name}.hdb"))
                        .UseInstantFlush()
                        .SetAutoShrink(false));
                else
                    q.UseMemoryQueues();
            })
            .Build();
    }

    private static HorseMessage Message(string id, bool priority = false, int size = 8)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, "x") { HighPriority = priority };
        message.SetMessageId(id);
        message.SetStringContent(new string('a', size));
        return message;
    }

    private static int Stored(HorseQueue queue) => queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task RejectNew_ConcurrentPushes_NeverExceedLimit(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("reject-race", o => o.MessageLimit = 3);

        PushResult[] results = await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => Task.Run(() => queue.Push(Message("m" + i)))));

        Assert.Equal(3, results.Count(x => x == PushResult.Success));
        Assert.Equal(37, results.Count(x => x == PushResult.LimitExceeded));
        Assert.Equal(3, Stored(queue));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task DeleteOldest_ConcurrentPushes_StayWithinLimit(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("delete-race", o =>
        {
            o.MessageLimit = 3;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
        });

        PushResult[] results = await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => Task.Run(() => queue.Push(Message("m" + i)))));

        Assert.All(results, x => Assert.Equal(PushResult.Success, x));
        Assert.Equal(3, Stored(queue));
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task DeleteOldest_PriorityOnlyMessages_StayWithinLimit(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("priority-only", o =>
        {
            o.MessageLimit = 1;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
        });

        for (int i = 0; i < 4; i++)
            Assert.Equal(PushResult.Success, await queue.Push(Message("p" + i, priority: true)));

        Assert.Equal(1, Stored(queue));
        Assert.Equal("p3", queue.Manager.PriorityMessageStore.ReadFirst().Message.MessageId);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task DeleteOldest_RemovesTheOldestMessageOfEitherStore(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("oldest-of-both", o =>
        {
            o.MessageLimit = 2;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
        });

        Assert.Equal(PushResult.Success, await queue.Push(Message("p-old", priority: true)));
        await Task.Delay(5);
        Assert.Equal(PushResult.Success, await queue.Push(Message("r-1")));
        await Task.Delay(5);
        Assert.Equal(PushResult.Success, await queue.Push(Message("r-2")));

        Assert.Equal(0, queue.Manager.PriorityMessageStore.Count());
        Assert.Equal(2, queue.Manager.MessageStore.Count());
        Assert.Equal("r-1", queue.Manager.MessageStore.ReadFirst().Message.MessageId);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task DeleteOldest_OversizedPush_DoesNotDeleteStoredMessage(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("oversized", o =>
        {
            o.MessageLimit = 1;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
            o.MessageSizeLimit = 100;
        });

        Assert.Equal(PushResult.Success, await queue.Push(Message("valid")));
        Assert.Equal(PushResult.LimitExceeded, await queue.Push(Message("oversized", size: 1000)));

        Assert.Equal(1, Stored(queue));
        Assert.Equal("valid", queue.Manager.MessageStore.ReadFirst().Message.MessageId);
    }

    [Theory]
    [InlineData("memory")]
    [InlineData("persistent")]
    public async Task DeleteOldest_DuplicatePush_DoesNotDeleteStoredMessage(string mode)
    {
        HorseRider rider = CreateRider(mode);
        HorseQueue queue = await rider.Queue.Create("duplicate", o =>
        {
            o.MessageLimit = 1;
            o.LimitExceededStrategy = MessageLimitExceededStrategy.DeleteOldestMessage;
            o.MessageIdUniqueCheck = true;
        });

        Assert.Equal(PushResult.Success, await queue.Push(Message("same")));
        Assert.Equal(PushResult.DuplicateUniqueId, await queue.Push(Message("same")));

        Assert.Equal(1, Stored(queue));
    }

    [Fact]
    public async Task RejectNew_FailedPush_ReleasesItsLimitSlot()
    {
        HorseRider rider = CreateRider("memory");
        HorseQueue queue = await rider.Queue.Create("slot-release", o =>
        {
            o.MessageLimit = 2;
            o.MessageIdUniqueCheck = true;
        });

        Assert.Equal(PushResult.Success, await queue.Push(Message("a")));

        // passes the limit check (1 stored + itself), then fails as a duplicate
        Assert.Equal(PushResult.DuplicateUniqueId, await queue.Push(Message("a")));

        // a leaked reservation would make this 1 stored + 1 leaked + itself > 2
        Assert.Equal(PushResult.Success, await queue.Push(Message("b")));
        Assert.Equal(PushResult.LimitExceeded, await queue.Push(Message("c")));
        Assert.Equal(2, Stored(queue));
    }
}
