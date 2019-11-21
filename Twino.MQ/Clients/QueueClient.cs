using System;
using Twino.MQ.Channels;

namespace Twino.MQ.Clients
{
    public class QueueClient
    {
        public DateTime SubscriptionDate { get; set; }
        public ChannelQueue Queue { get; set; }
        public MqClient Client { get; set; }
    }
}