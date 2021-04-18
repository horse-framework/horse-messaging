using System;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    public class ChannelClient
    {
        public HorseChannel Channel { get; }
        public MessagingClient Client { get; }
        public DateTime SubscribedAt { get; }

        public ChannelClient(HorseChannel channel, MessagingClient client)
        {
            Channel = channel;
            Client = client;
            SubscribedAt = DateTime.UtcNow;
        }
    }
}