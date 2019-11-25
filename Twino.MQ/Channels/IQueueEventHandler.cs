using System.Threading.Tasks;
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
        Task OnSubscription(ChannelQueue queue, QueueClient client);

        /// <summary>
        /// Called when a client is unsubscribed from the queue
        /// </summary>
        Task OnUnsubscription(ChannelQueue queue, QueueClient client);

        /// <summary>
        /// Called when a new message is added to the queue
        /// </summary>
        Task OnMessageAdded(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when a message removed from the queue
        /// </summary>
        Task OnMessageRemoved(ChannelQueue queue, QueueMessage message);

        /// <summary>
        /// Called when queue status has changed
        /// </summary>
        Task<bool> OnStatusChanged(ChannelQueue queue, QueueStatus from, QueueStatus to);
    }
}