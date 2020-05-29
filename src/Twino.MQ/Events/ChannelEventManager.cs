using System;
using System.Threading.Tasks;

namespace Twino.MQ.Events
{
    public class ChannelEventManager : EventManager
    {
        public ChannelEventManager(string eventName, MqServer server)
            : base(eventName, null, 0)
        {
        }

        public async Task Trigger(Channel channel)
        {
            throw new NotImplementedException();
        }
    }
}