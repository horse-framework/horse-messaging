using System;
using Twino.MQ.Queues;

namespace Twino.MQ.Clients
{
    /// <summary>
    /// Definition object of a client in a channel
    /// </summary>
    public class QueueClient
    {
        /// <summary>
        /// The time when client has joined to the channel
        /// </summary>
        public DateTime JoinDate { get; }

        /// <summary>
        /// Queue object
        /// </summary>
        public TwinoQueue Queue { get; set; }

        /// <summary>
        /// Client object
        /// </summary>
        public MqClient Client { get; set; }

        /// <summary>
        /// Creates new channel client pair descriptor
        /// </summary>
        public QueueClient(TwinoQueue queue, MqClient client)
        {
            Queue = queue;
            Client = client;
            JoinDate = DateTime.UtcNow;
        }
    }
}