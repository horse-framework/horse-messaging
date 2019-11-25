using System;
using Twino.MQ.Channels;

namespace Twino.MQ.Clients
{
    /// <summary>
    /// Definition object of a client in a channel
    /// </summary>
    public class ChannelClient
    {
        /// <summary>
        /// The time when client has joined to the channel
        /// </summary>
        public DateTime JoinDate { get; set; }
        
        /// <summary>
        /// Channel object
        /// </summary>
        public Channel Channel { get; set; }
        
        /// <summary>
        /// Client object
        /// </summary>
        public MqClient Client { get; set; }
    }
}