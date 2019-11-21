using System;
using Twino.MQ.Channels;

namespace Twino.MQ.Clients
{
    public class ChannelClient
    {
        public DateTime JoinDate { get; set; }
        public Channel Channel { get; set; }
        public MqClient Client { get; set; }
    }
}