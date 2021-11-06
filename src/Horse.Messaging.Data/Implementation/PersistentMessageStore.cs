using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Store;

namespace Horse.Messaging.Data.Implementation
{
    /// <summary>
    /// Message store object for persistent queues
    /// </summary>
    public class PersistentMessageStore : LinkedMessageStore
    {
        internal Database Database { get; }

        /// <summary>
        /// Creates new persistent message store
        /// </summary>
        public PersistentMessageStore(IHorseQueueManager manager, DatabaseOptions databaseOptions)
            : base(manager)
        {
            Database = new Database(databaseOptions);
        }

        /// <summary>
        /// Initializes the store, opens the database file.
        /// </summary>
        public Task Initialize()
        {
            return Database.Open();
        }

        /// <inheritdoc />
        public override bool Remove(string messageId)
        {
            Database.Delete(messageId).GetAwaiter().GetResult();
            return base.Remove(messageId);
        }

        /// <inheritdoc />
        public override void Remove(HorseMessage message)
        {
            Database.Delete(message.MessageId).GetAwaiter().GetResult();
            base.Remove(message);
        }

        /// <inheritdoc />
        public override void Remove(QueueMessage message)
        {
            Database.Delete(message.Message.MessageId).GetAwaiter().GetResult();
            base.Remove(message);
        }

        /// <summary>
        /// Removes the message from disk and queue
        /// </summary>
        public async Task<bool> RemoveMessage(QueueMessage message)
        {
            await Database.Delete(message.Message.MessageId);
            base.Remove(message);
            return true;
        }

        internal void RemoveFromOnlyMemory(QueueMessage message)
        {
            base.Remove(message);
        }
        
        /// <summary>
        /// Remoes the message from disk and queue
        /// </summary>
        public async Task<bool> RemoveMessage(string messageId)
        {
            await Database.Delete(messageId);
            base.Remove(messageId);
            return true;
        }

        /// <inheritdoc />
        public override async Task Clear()
        {
            await Database.Clear();
            await base.Clear();
        }

        /// <inheritdoc />
        public override async Task Destroy()
        {
            await Database.RemoveDatabase();
            await base.Destroy();
        }
    }
}