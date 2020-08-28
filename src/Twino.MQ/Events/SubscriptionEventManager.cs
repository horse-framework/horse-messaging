using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Manages client join and leave events
    /// </summary>
    public class SubscriptionEventManager : EventManager
    {
        /// <summary>
        /// Creates new client event manager
        /// </summary>
        public SubscriptionEventManager(TwinoMQ server, string eventName, TwinoQueue queue)
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