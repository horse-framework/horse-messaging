using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Queues.Delivery
{
    /// <summary>
    /// Delivery tracker implementation for queue messages 
    /// </summary>
    public interface IDeliveryTracker
    {
        /// <summary>
        /// That method is called once when the queue is initialized.
        /// </summary>
        void Start();

        /// <summary>
        /// Resets all tracking messages
        /// </summary>
        void Reset();

        /// <summary>
        /// That method is called when the queue is destroyed.
        /// All resources should be released in here.
        /// </summary>
        Task Destroy();

        /// <summary>
        /// Starts to track new message delivery
        /// </summary>
        void Track(MessageDelivery delivery);

        /// <summary>
        /// Returns all tracking queue messages
        /// </summary>
        List<QueueMessage> GetDeliveringMessages();

        /// <summary>
        /// Finds a tracking delivery by message id and the consumer client that received the queue message.
        /// </summary>
        MessageDelivery FindDelivery(MessagingClient client, string messageId);

        /// <summary>
        /// Finds a tracking delivery by message id and the consumer client that received the queue message.
        /// That method also removes delivery from tracking deliveries list.
        /// </summary>
        MessageDelivery FindAndRemoveDelivery(MessagingClient client, string messageId);
        
        /// <summary>
        /// Untracks the delivery and removes it from delivering messages list.
        /// </summary>
        void RemoveDelivery(MessageDelivery delivery);
 
        /// <summary>
        /// Returns actively tracking deliveries count
        /// </summary>
        int GetDeliveryCount();
    }
}