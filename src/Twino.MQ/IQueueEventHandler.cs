using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Twino.MQ
{
    /// <summary>
    /// Channel event handler implementation (client join/leave, queue created/removed, status changes)
    /// </summary>
    public interface IQueueEventHandler
    {
        /// <summary>
        /// Called when a new queue is created in a channel
        /// </summary>
        Task OnCreated(TwinoQueue queue);

        /// <summary>
        /// Called when a queue is removed from a channel
        /// </summary>
        Task OnRemoved(TwinoQueue queue);

        /// <summary>
        /// Called when a client joined to the channel
        /// </summary>
        Task OnConsumerSubscribed(QueueClient client);

        /// <summary>
        /// Called when a client left from the channel
        /// </summary>
        Task OnConsumerUnsubscribed(QueueClient client);

        /// <summary>
        /// Called when queue status has changed
        /// </summary>
        Task OnStatusChanged(TwinoQueue queue, QueueStatus from, QueueStatus to);
    }
}