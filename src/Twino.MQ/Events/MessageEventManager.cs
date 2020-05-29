using System;
using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Events
{
    public class MessageEventManager : EventManager
    {
        public MessageEventManager(string eventName, ChannelQueue queue)
            : base(eventName, queue.Channel.Name, queue.Id)
        {
        }

        public async Task Trigger(QueueMessage message)
        {
            throw new NotImplementedException();
        }
    }
}