using System.Threading.Tasks;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Messages event manager.
    /// Manages events when a message is processed in a queue
    /// </summary>
    public class MessageEventManager : EventManager
    {
        /// <summary>
        /// Creates new message event manager
        /// </summary>
        public MessageEventManager(TwinoMQ server, string eventName, TwinoQueue queue)
            : base(server, eventName, queue.Name)
        {
        }

        /// <summary>
        /// Triggers when a message is produced
        /// </summary>
        public void Trigger(QueueMessage message)
        {
            base.Trigger(new MessageEvent
                         {
                             Id = message.Message.MessageId,
                             Queue = message.Message.ContentType,
                             Channel = message.Message.Target,
                             Saved = message.IsSaved,
                             ProducerId = message.Source?.UniqueId,
                             ProducerName = message.Source?.Name,
                             ProducerType = message.Source?.Type
                         });
        }
    }
}