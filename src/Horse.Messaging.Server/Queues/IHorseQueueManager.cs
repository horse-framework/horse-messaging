using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Server.Queues
{
    public interface IHorseQueueManager
    {
        HorseQueue Queue { get; }

        IQueueMessageStore MessageStore { get; }

        IQueueMessageStore PriorityMessageStore { get; }

        IQueueDeliveryHandler DeliveryHandler { get; }

        IQueueSynchronizer Synchronizer { get; }

        Task Initialize();

        Task Destroy();

        Task OnExceptionThrown(string hint, QueueMessage message, Exception exception);

        Task OnMessageTimeout(QueueMessage message);
        
        bool AddMessage(QueueMessage message);

        Task<bool> RemoveMessage(QueueMessage message);

        Task<bool> RemoveMessage(string messageId);

        Task<bool> SaveMessage(QueueMessage message);

        Task<bool> ChangeMessagePriority(QueueMessage message, bool priority);
    }
}