using Twino.MQ.Clients;

namespace Twino.MQ.Channels
{
    /// <summary>
    /// Messaging queue event handler implementation (subs, unsubs, message add/remove, status changes)
    /// </summary>
    public interface IQueueEventHandler
    {
        /// <summary>
        /// Called when a client is subscribed to the queue
        /// </summary>
        void OnSubscription(ChannelQueue queue, QueueClient client);

        /// <summary>
        /// Called when a client is unsubscribed from the queue
        /// </summary>
        void OnUnsubscription(ChannelQueue queue, QueueClient client);

        /// <summary>
        /// Called when a new message is added to the queue
        /// </summary>
        void OnMessageAdded(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when a message removed from the queue
        /// </summary>
        void OnMessageRemoved(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when queue status has changed
        /// </summary>
        void OnStatusChanged(ChannelQueue queue, QueueStatus from, QueueStatus to);
    }
}