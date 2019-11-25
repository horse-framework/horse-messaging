using System;
using Twino.MQ.Channels;

namespace Twino.MQ.Clients
{
    /// <summary>
    /// Definition object of a client that subscribed to a queue
    /// </summary>
    public class QueueClient
    {
        /// <summary>
        /// The time when client has susbcribed to the queue
        /// </summary>
        public DateTime SubscriptionDate { get; set; }
        
        /// <summary>
        /// Queue object
        /// </summary>
        public ChannelQueue Queue { get; set; }
        
        /// <summary>
        /// Client object
        /// </summary>
        public MqClient Client { get; set; }
    }
}