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
        public SubscriptionEventManager(string eventName, Channel channel)
            : base(eventName, channel.Name, 0)
        {
        }

        /// <summary>
        /// Triggers client joined or left channel events
        /// </summary>
        public Task Trigger(ChannelClient client, string node = null)
        {
            return base.Trigger(new SubscriptionEvent
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