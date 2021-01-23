using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;

namespace Horse.Mq
{
    /// <summary>
    /// Queue event handler implementation (client join/leave, queue created/removed, status changes)
    /// </summary>
    public interface IQueueEventHandler
    {
        /// <summary>
        /// Called when a new queue is created
        /// </summary>
        Task OnCreated(HorseQueue queue);

        /// <summary>
        /// Called when a queue is removed
        /// </summary>
        Task OnRemoved(HorseQueue queue);

        /// <summary>
        /// Called when a client subscribes to the queue
        /// </summary>
        Task OnConsumerSubscribed(QueueClient client);

        /// <summary>
        /// Called when a client unsubscribes from the queue
        /// </summary>
        Task OnConsumerUnsubscribed(QueueClient client);

        /// <summary>
        /// Called when queue status has changed
        /// </summary>
        Task OnStatusChanged(HorseQueue queue, QueueStatus from, QueueStatus to);
    }
}