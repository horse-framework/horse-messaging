using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Store;
using Xunit;

namespace Test.Queues.Store;

/// <summary>
/// A persistent queue keeps priority and regular messages in two files. The priority of a message
/// can change after it is saved (priority putback, cluster priority change) while its record stays
/// in the file it was written to. Deletes chosen by the current priority went to the other file,
/// and the deleted message came back after restart.
/// </summary>
public class PersistentPriorityRecordTest : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "ppr-" + Guid.NewGuid().ToString("N"));

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

    private HorseRider StartRider()
    {
        Directory.CreateDirectory(_path);
        return HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = _path)
            .ConfigureQueues(q =>
            {
                q.Options.Type = QueueType.Push;
                q.Options.CommitWhen = CommitWhen.None;
                q.Options.Acknowledge = QueueAckDecision.None;
                q.UsePersistentQueues(db => db.SetPhysicalPath(queue => Path.Combine(_path, $"{queue.Name}.hdb"))
                    .UseInstantFlush()
                    .SetAutoShrink(false));
            })
            .Build();
    }

    private static async Task StopRider(HorseRider rider)
    {
        foreach (HorseQueue queue in rider.Queue.Queues.ToArray())
            if (queue.Manager != null)
                await queue.Manager.Flush();
    }

    private static HorseMessage Message(string id, bool priority)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, "x") { HighPriority = priority };
        message.SetMessageId(id);
        message.SetStringContent("payload");
        return message;
    }

    private async Task<int> CountAfterRestart(string queueName)
    {
        HorseRider rider = StartRider();
        try
        {
            HorseQueue queue = rider.Queue.Find(queueName);
            return queue.Manager.MessageStore.Count() + queue.Manager.PriorityMessageStore.Count();
        }
        finally
        {
            await StopRider(rider);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PriorityChangedByPutBack_DeletedMessage_DoesNotComeBackAfterRestart(bool savedAsPriority)
    {
        HorseRider rider = StartRider();
        HorseQueue queue = await rider.Queue.Create("putback-delete");
        Assert.Equal(PushResult.Success, await queue.Push(Message("m1", savedAsPriority)));

        QueueMessage message = StoreOf(queue, savedAsPriority).ConsumeFirst();
        Assert.Equal(savedAsPriority, message.SavedToPriorityStore);

        // NACK or ack timeout with the other priority: the message moves to the other store in memory
        await queue.ApplyDecision(Decision.PutBackMessage(savedAsPriority), message);
        Assert.Equal(!savedAsPriority, message.Message.HighPriority);
        Assert.Same(message, StoreOf(queue, !savedAsPriority).ReadFirst());

        // later ACK
        await queue.ApplyDecision(Decision.DeleteMessage(), message);
        Assert.True(message.IsRemoved);
        await StopRider(rider);

        Assert.Equal(0, await CountAfterRestart("putback-delete"));
    }

    [Fact]
    public async Task PriorityChangedByPutBack_TimedOutMessage_DoesNotComeBackAfterRestart()
    {
        HorseRider rider = StartRider();
        HorseQueue queue = await rider.Queue.Create("putback-timeout");
        Assert.Equal(PushResult.Success, await queue.Push(Message("m1", false)));

        QueueMessage message = queue.Manager.MessageStore.ConsumeFirst();
        await queue.ApplyDecision(Decision.PutBackMessage(false), message);

        // The message timeout tracker removes it from the store it is in now
        queue.Manager.PriorityMessageStore.Remove(message);
        await StopRider(rider);

        Assert.Equal(0, await CountAfterRestart("putback-timeout"));
    }

    [Fact]
    public async Task RemoveById_PriorityMessage_IsRemovedFromMemoryAndDisk()
    {
        HorseRider rider = StartRider();
        HorseQueue queue = await rider.Queue.Create("remove-priority-id");
        Assert.Equal(PushResult.Success, await queue.Push(Message("p1", true)));
        Assert.Equal(PushResult.Success, await queue.Push(Message("r1", false)));

        // Replicas remove messages this way
        await queue.RemoveMessage("p1");

        Assert.Equal(0, queue.Manager.PriorityMessageStore.Count());
        Assert.Equal(1, queue.Manager.MessageStore.Count());
        await StopRider(rider);

        HorseRider restarted = StartRider();
        try
        {
            HorseQueue loaded = restarted.Queue.Find("remove-priority-id");
            Assert.Equal(0, loaded.Manager.PriorityMessageStore.Count());
            Assert.Equal("r1", loaded.Manager.MessageStore.ReadFirst().Message.MessageId);
        }
        finally
        {
            await StopRider(restarted);
        }
    }

    [Fact]
    public async Task RemoveById_RegularMessage_DoesNotTouchPriorityMessages()
    {
        HorseRider rider = StartRider();
        HorseQueue queue = await rider.Queue.Create("remove-regular-id");
        Assert.Equal(PushResult.Success, await queue.Push(Message("p1", true)));
        Assert.Equal(PushResult.Success, await queue.Push(Message("r1", false)));

        await queue.RemoveMessage("r1");

        Assert.Equal(1, queue.Manager.PriorityMessageStore.Count());
        Assert.Equal(0, queue.Manager.MessageStore.Count());
        await StopRider(rider);

        Assert.Equal(1, await CountAfterRestart("remove-regular-id"));
    }

    [Fact]
    public async Task OrphanDeleteWrittenByOlderVersion_IsAppliedToTheOtherFileOnLoad()
    {
        HorseRider rider = StartRider();
        HorseQueue queue = await rider.Queue.Create("orphan-delete");
        Assert.Equal(PushResult.Success, await queue.Push(Message("gone", false)));
        Assert.Equal(PushResult.Success, await queue.Push(Message("kept", false)));
        await StopRider(rider);

        // What older versions left behind: the delete of a regular record written into the priority file
        Database priorityFile = new Database(new DatabaseOptions
        {
            Filename = Path.Combine(_path, "orphan-delete_prio.hdb"),
            InstantFlush = true
        });
        await priorityFile.Open();
        await priorityFile.Delete("gone");
        await priorityFile.CloseWithoutShrink();

        HorseRider restarted = StartRider();
        HorseQueue loaded = restarted.Queue.Find("orphan-delete");
        Assert.Equal(1, loaded.Manager.MessageStore.Count());
        Assert.Equal("kept", loaded.Manager.MessageStore.ReadFirst().Message.MessageId);
        await StopRider(restarted);

        // The fix is written into the regular file, so it holds after the orphan delete is gone
        Assert.Equal(1, await CountAfterRestart("orphan-delete"));
    }

    [Fact]
    public async Task DeleteAndSaveAgainInSameFile_IsNotTreatedAsOrphan()
    {
        HorseRider rider = StartRider();
        HorseQueue queue = await rider.Queue.Create("same-file");
        Assert.Equal(PushResult.Success, await queue.Push(Message("p-del", true)));
        Assert.Equal(PushResult.Success, await queue.Push(Message("r-live", false)));
        await queue.RemoveMessage("p-del");
        await StopRider(rider);

        Assert.Equal(1, await CountAfterRestart("same-file"));
    }

    private static IQueueMessageStore StoreOf(HorseQueue queue, bool priority)
        => priority ? queue.Manager.PriorityMessageStore : queue.Manager.MessageStore;
}
