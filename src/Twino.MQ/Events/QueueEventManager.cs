using System.Threading.Tasks;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Queue event manager.
    /// Manages queue created, updated, deleted events
    /// </summary>
    public class QueueEventManager : EventManager
    {
        /// <summary>
        /// Creates new queue event manager
        /// </summary>
        public QueueEventManager(string eventName, Channel channel)
            : base(eventName, channel.Name, 0)
        {
        }

        /// <summary>
        /// Triggers queue created, updated or deleted events
        /// </summary>
        public Task Trigger(ChannelQueue queue, string node = null)
        {
            return base.Trigger(new QueueEvent
                                {
                                    Id = queue.Id,
                                    Tag = queue.TagName,
                                    Channel = queue.Channel.Name,
                                    Status = queue.Status.ToString(),
                                    Messages = queue.MessageCount(),
                                    PriorityMessages = queue.PriorityMessageCount(),
                                    Node = node
                                });
        }
    }
}