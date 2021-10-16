using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Server.Queues.Managers
{
    public class MemoryQueueManager : IHorseQueueManager
    {
        public HorseQueue Queue { get; }
        public IQueueMessageStore MessageStore { get; protected set; }
        public IQueueMessageStore PriorityMessageStore { get; protected set; }
        public IQueueDeliveryHandler DeliveryHandler { get; protected set; }
        public IQueueSynchronizer Synchronizer { get; protected set; }

        public MemoryQueueManager(HorseQueue queue)
        {
            Queue = queue;

            MessageStore = new LinkedMessageStore(this);
            PriorityMessageStore = new LinkedMessageStore(this);
            Synchronizer = new MemoryQueueSynchronizer(this);
            DeliveryHandler = new MemoryDeliveryHandler(this);
        }

        public virtual Task Initialize()
        {
            DeliveryHandler.Tracker.Start();
            PriorityMessageStore.TimeoutTracker.Start();
            MessageStore.TimeoutTracker.Start();
            return Task.CompletedTask;
        }

        public virtual async Task Destroy()
        {
            PriorityMessageStore.TimeoutTracker.Stop();
            MessageStore.TimeoutTracker.Stop();

            await DeliveryHandler.Tracker.Destroy();
            await PriorityMessageStore.Destroy();
            await MessageStore.Destroy();
        }

        public virtual Task OnExceptionThrown(string hint, QueueMessage message, Exception exception)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnMessageTimeout(QueueMessage message)
        {
            return Task.CompletedTask;
        }

        #region Add- Remove

        public virtual bool AddMessage(QueueMessage message)
        {
            if (message.Message.HighPriority)
                PriorityMessageStore.Put(message);
            else
                MessageStore.Put(message);

            return true;
        }

        public virtual Task<bool> RemoveMessage(QueueMessage message)
        {
            if (message.IsInQueue)
            {
                if (message.Message.HighPriority)
                    PriorityMessageStore.Remove(message);
                else
                    MessageStore.Remove(message);
            }

            return Task.FromResult(true);
        }

        public virtual Task<bool> RemoveMessage(string messageId)
        {
            bool removed = PriorityMessageStore.Remove(messageId);
            if (removed)
                return Task.FromResult(true);

            removed = MessageStore.Remove(messageId);

            return Task.FromResult(removed);
        }

        public virtual Task<bool> SaveMessage(QueueMessage message)
        {
            return Task.FromResult(false);
        }

        public virtual Task<bool> ChangeMessagePriority(QueueMessage message, bool priority)
        {
            if (message.Message.HighPriority == priority)
                return Task.FromResult(false);

            message.Message.HighPriority = priority;
            if (priority)
            {
                MessageStore.Remove(message);
                PriorityMessageStore.Put(message);
            }
            else
            {
                PriorityMessageStore.Remove(message);
                MessageStore.Put(message);
            }

            return Task.FromResult(true);
        }

        #endregion
    }
}