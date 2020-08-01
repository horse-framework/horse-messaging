using System.Threading.Tasks;
using Twino.MQ.Clients;
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
        public SubscriptionEventManager(TwinoMQ server, string eventName, Channel channel)
            : base(server, eventName, channel.Name, 0)
        {
        }

        /// <summary>
        /// Triggers client joined or left channel events
        /// </summary>
        public void Trigger(ChannelClient client, string node = null)
        {
            base.Trigger(new SubscriptionEvent
                         {
                             Channel = client.Channel.Name,
                             ClientId = client.Client.UniqueId,
                             ClientName = client.Client.Name,
                             ClientType = client.Client.Type,
                             Node = node
                         });
        }
    }
}