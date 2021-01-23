using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq.Models.Events;

namespace Horse.Mq.Events
{
    /// <summary>
    /// Manages client join and leave events
    /// </summary>
    public class SubscriptionEventManager : EventManager
    {
        /// <summary>
        /// Creates new client event manager
        /// </summary>
        public SubscriptionEventManager(HorseMq server, string eventName, HorseQueue queue)
            : base(server, eventName, queue.Name)
        {
        }

        /// <summary>
        /// Triggers client subscribes/unsubscribes queue events
        /// </summary>
        public void Trigger(QueueClient client, string node = null)
        {
            base.Trigger(new SubscriptionEvent
                         {
                             Queue = client.Queue.Name,
                             ClientId = client.Client.UniqueId,
                             ClientName = client.Client.Name,
                             ClientType = client.Client.Type,
                             Node = node
                         });
        }
    }
}