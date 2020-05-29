using System;
using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Events
{
    public class QueueEventManager : EventManager
    {
        public QueueEventManager(string eventName, Channel channel)
            : base(eventName, channel.Name, 0)
        {
        }

        public async Task Trigger(ChannelQueue queue)
        {
            throw new NotImplementedException();
        }
    }
}