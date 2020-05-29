using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Manages channel - client relational events
    /// </summary>
    public class SubscriptionEventManager : EventManager
    {
        public SubscriptionEventManager(string eventName, Channel channel)
            : base(eventName, channel.Name, 0)
        {
        }

        public async Task Trigger(ChannelClient client)
        {
            throw new NotImplementedException();
        }
    }
}