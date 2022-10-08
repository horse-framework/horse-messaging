using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Store
{
    /// <summary>
    /// Queue message store implementation stores queue messages.
    /// </summary>
    public interface IQueueMessageStore
    {
        /// <summary>
        /// Manager of the queue
        /// </summary>
        IHorseQueueManager Manager { get; }
        
        /// <summary>
        /// Message timeout tracker of the store
        /// </summary>
        IMessageTimeoutTracker TimeoutTracker { get; }
        
        /// <summary>
        /// Returns true if there is no message in the store
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Returns count of all stored messages
        /// </summary>
        /// <returns></returns>
        int Count();

        /// <summary>
        /// Puts a message into message store 
        /// </summary>
        void Put(QueueMessage message);

        /// <summary>
        /// Gets next message from store
        /// </summary>
        QueueMessage ReadFirst();

        /// <summary>
        /// Gets next message from store
        /// </summary>
        QueueMessage ConsumeFirst();

        /// <summary>
        /// Finds a message by Id
        /// </summary>
        QueueMessage Find(string messageId);
        
        /// <summary>
        /// Gets next message from store
        /// </summary>
        List<QueueMessage> ConsumeMultiple(int count);
        
        /// <summary>
        /// Gets all messages.
        /// That method returns the messages without thread safe
        /// </summary>
        IEnumerable<QueueMessage> GetUnsafe();
        
        /// <summary>
        /// Finds and removes message from store
        /// </summary>
        bool Remove(string messageId);

        /// <summary>
        /// Finds and removes message from store
        /// </summary>
        void Remove(HorseMessage message);

        /// <summary>
        /// Finds and removes message from store
        /// </summary>
        void Remove(QueueMessage message);

        /// <summary>
        /// Clears all messages from store
        /// </summary>
        Task Clear();

        /// <summary>
        /// Destroys all messages
        /// </summary>
        /// <returns></returns>
        Task Destroy();
    }
}