using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;
using Xunit;

namespace Test.Queues.Store;

public class LinkedMessageStoreTest
{
    [Fact]
    public void RemoveById_AllowsReinsertingSameMessageInstance()
    {
        LinkedMessageStore store = CreateStore();
        QueueMessage message = CreateMessage("rm-id-1");

        store.Put(message);
        Assert.True(store.Remove("rm-id-1"));
        Assert.Equal(0, store.Count());

        store.Put(message);

        Assert.Equal(1, store.Count());
        Assert.Same(message, store.ReadFirst());
    }

    [Fact]
    public void RemoveByMessage_AllowsReinsertingSameMessageInstance()
    {
        LinkedMessageStore store = CreateStore();
        QueueMessage message = CreateMessage("rm-msg-1");

        store.Put(message);
        store.Remove(message);
        Assert.Equal(0, store.Count());

        store.Put(message);

        Assert.Equal(1, store.Count());
        Assert.Same(message, store.ReadFirst());
    }

    [Fact]
    public async Task Clear_ResetsQueueMembershipForExistingInstances()
    {
        LinkedMessageStore store = CreateStore();
        QueueMessage first = CreateMessage("clear-1");
        QueueMessage second = CreateMessage("clear-2");

        store.Put(first);
        store.Put(second);

        await store.Clear();
        Assert.True(store.IsEmpty);

        store.Put(first);
        store.Put(second);

        Assert.Equal(2, store.Count());
    }

    [Fact]
    public void GetUnsafe_ReturnsCurrentItems()
    {
        LinkedMessageStore store = CreateStore();
        store.Put(CreateMessage("snap-1"));
        store.Put(CreateMessage("snap-2"));

        List<string> ids = new List<string>();
        foreach (QueueMessage message in store.GetUnsafe())
            ids.Add(message.Message.MessageId);

        Assert.Equal(new[] {"snap-1", "snap-2"}, ids);
    }

    private static LinkedMessageStore CreateStore()
    {
        return new LinkedMessageStore(new FakeQueueManager());
    }

    private static QueueMessage CreateMessage(string messageId)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, "test-queue");
        message.SetMessageId(messageId);
        message.SetStringContent(messageId);
        return new QueueMessage(message);
    }

    private sealed class FakeQueueManager : IHorseQueueManager
    {
        public HorseQueue Queue => null;
        public IQueueMessageStore MessageStore => null;
        public IQueueMessageStore PriorityMessageStore => null;
        public IQueueDeliveryHandler DeliveryHandler => null;
        public IQueueSynchronizer Synchronizer => null;
        public Task Initialize() => Task.CompletedTask;
        public Task Destroy() => Task.CompletedTask;
        public Task OnMessageTimeout(QueueMessage message) => Task.CompletedTask;
        public bool AddMessage(QueueMessage message) => true;
        public Task<bool> RemoveMessage(QueueMessage message) => Task.FromResult(true);
        public Task<bool> RemoveMessage(string messageId) => Task.FromResult(true);
        public Task<bool> SaveMessage(QueueMessage message) => Task.FromResult(false);
        public Task<bool> ChangeMessagePriority(QueueMessage message, bool priority) => Task.FromResult(false);
    }
}
