using System;

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
        public DateTime JoinDate { get; }

        /// <summary>
        /// Channel object
        /// </summary>
        public Channel Channel { get; set; }

        /// <summary>
        /// Client object
        /// </summary>
        public MqClient Client { get; set; }

        public ChannelClient(Channel channel, MqClient client)
        {
            Channel = channel;
            Client = client;
            JoinDate = DateTime.UtcNow;
        }
    }
}