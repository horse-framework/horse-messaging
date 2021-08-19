using System;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// Channel client object
    /// </summary>
    public class ChannelClient
    {
        /// <summary>
        /// Channel
        /// </summary>
        public HorseChannel Channel { get; }
        
        /// <summary>
        /// Client
        /// </summary>
        public MessagingClient Client { get; }
        
        /// <summary>
        /// Subscription date
        /// </summary>
        public DateTime SubscribedAt { get; }

        /// <summary>
        /// Creates new channel client object
        /// </summary>
        public ChannelClient(HorseChannel channel, MessagingClient client)
        {
            Channel = channel;
            Client = client;
            SubscribedAt = DateTime.UtcNow;
        }
    }
}