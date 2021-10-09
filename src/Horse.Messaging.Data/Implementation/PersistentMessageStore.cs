using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Store;

namespace Horse.Messaging.Data.Implementation
{
    public class PersistentMessageStore : LinkedMessageStore
    {
        internal Database Database { get; }

        public PersistentMessageStore(IHorseQueueManager manager, DatabaseOptions databaseOptions)
            : base(manager)
        {
            Database = new Database(databaseOptions);
        }

        public Task Initialize()
        {
            return Database.Open();
        }

        public override bool Remove(string messageId)
        {
            Database.Delete(messageId).GetAwaiter().GetResult();
            return base.Remove(messageId);
        }

        public override void Remove(HorseMessage message)
        {
            Database.Delete(message.MessageId).GetAwaiter().GetResult();
            base.Remove(message);
        }

        public override void Remove(QueueMessage message)
        {
            Database.Delete(message.Message.MessageId).GetAwaiter().GetResult();
            base.Remove(message);
        }

        public async Task<bool> RemoveMessage(QueueMessage message)
        {
            await Database.Delete(message.Message.MessageId);
            base.Remove(message);
            return true;
        }

        public async Task<bool> RemoveMessage(string messageId)
        {
            await Database.Delete(messageId);
            base.Remove(messageId);
            return true;
        }

        public override async Task Clear()
        {
            await Database.Clear();
            await base.Clear();
        }

        public override async Task Destroy()
        {
            await Database.RemoveDatabase();
            await base.Destroy();
        }
    }
}