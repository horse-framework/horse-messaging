using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Server.Queues.Managers
{
    /// <summary>
    /// Default memory queue manager implementation for non persistent queues
    /// </summary>
    public class MemoryQueueManager : IHorseQueueManager
    {
        /// <inheritdoc />
        public HorseQueue Queue { get; }

        /// <inheritdoc />
        public IQueueMessageStore MessageStore { get; protected set; }

        /// <inheritdoc />
        public IQueueMessageStore PriorityMessageStore { get; protected set; }

        /// <inheritdoc />
        public IQueueDeliveryHandler DeliveryHandler { get; protected set; }

        /// <inheritdoc />
        public IQueueSynchronizer Synchronizer { get; protected set; }

        /// <summary>
        /// Creates new memory queue manager
        /// </summary>
        public MemoryQueueManager(HorseQueue queue)
        {
            Queue = queue;

            MessageStore = new DictionaryMessageStore(this);
            PriorityMessageStore = new DictionaryMessageStore(this);
            Synchronizer = new DefaultQueueSynchronizer(this);
            DeliveryHandler = new MemoryDeliveryHandler(this);
        }

        /// <inheritdoc />
        public virtual Task Initialize()
        {
            DeliveryHandler.Tracker.Start();
            PriorityMessageStore.TimeoutTracker.Start();
            MessageStore.TimeoutTracker.Start();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual async Task Destroy()
        {
            PriorityMessageStore.TimeoutTracker.Stop();
            MessageStore.TimeoutTracker.Stop();

            await DeliveryHandler.Tracker.Destroy();
            await PriorityMessageStore.Destroy();
            await MessageStore.Destroy();
        }

        /// <inheritdoc />
        public virtual Task OnExceptionThrown(string hint, QueueMessage message, Exception exception)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task OnMessageTimeout(QueueMessage message)
        {
            return Task.CompletedTask;
        }

        #region Add- Remove

        /// <inheritdoc />
        public virtual bool AddMessage(QueueMessage message)
        {
            if (message.Message.HighPriority)
                PriorityMessageStore.Put(message);
            else
                MessageStore.Put(message);

            return true;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public virtual Task<bool> RemoveMessage(string messageId)
        {
            bool removed = PriorityMessageStore.Remove(messageId);
            if (removed)
                return Task.FromResult(true);

            removed = MessageStore.Remove(messageId);

            return Task.FromResult(removed);
        }

        /// <inheritdoc />
        public virtual Task<bool> SaveMessage(QueueMessage message)
        {
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public virtual Task<bool> ChangeMessagePriority(QueueMessage message, bool priority)
        {
            if (message.Message.HighPriority == priority)
                return Task.FromResult(false);

            bool oldValue = message.Message.HighPriority;
            message.Message.HighPriority = priority;
            
            try
            {
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
            catch
            {
                message.Message.HighPriority = oldValue;
                return Task.FromResult(false);
            }
        }

        #endregion
    }
}