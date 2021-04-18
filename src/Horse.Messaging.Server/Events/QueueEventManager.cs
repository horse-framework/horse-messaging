using Horse.Messaging.Protocol.Models.Events;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Events
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
        public QueueEventManager(HorseMq server, string eventName)
            : base(server, eventName, null)
        {
        }

        /// <summary>
        /// Triggers queue created, updated or deleted events
        /// </summary>
        public void Trigger(HorseQueue queue, string node = null)
        {
            base.Trigger(new QueueEvent
                         {
                             Name = queue.Name,
                             Topic = queue.Topic,
                             Status = queue.Status.ToString(),
                             Messages = queue.MessageCount(),
                             PriorityMessages = queue.PriorityMessageCount(),
                             Node = node
                         });
        }
    }
}