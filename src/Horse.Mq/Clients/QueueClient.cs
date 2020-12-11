using System;
using Horse.Mq.Queues;

namespace Horse.Mq.Clients
{
    /// <summary>
    /// Definition object of a client subscribed to a queue
    /// </summary>
    public class QueueClient
    {
        /// <summary>
        /// The time when client has subscribed to the queue
        /// </summary>
        public DateTime JoinDate { get; }

        /// <summary>
        /// Queue object
        /// </summary>
        public HorseQueue Queue { get; set; }

        /// <summary>
        /// Client object
        /// </summary>
        public MqClient Client { get; set; }

        /// <summary>
        /// Used when acknowledge is enabled for the queue.
        /// When consumer receives a message, this value keeps the message until consumer sends ack/nack or ack time out. 
        /// </summary>
        public QueueMessage CurrentlyProcessing { get; internal set; }

        /// <summary>
        /// Calculated deadline for acknowledge timeout of the message
        /// </summary>
        public DateTime ProcessDeadline { get; set; }

        /// <summary>
        /// Creates new queue client pair descriptor
        /// </summary>
        public QueueClient(HorseQueue queue, MqClient client)
        {
            Queue = queue;
            Client = client;
            JoinDate = DateTime.UtcNow;
        }
    }
}