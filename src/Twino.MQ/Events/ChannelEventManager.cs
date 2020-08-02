using System.Threading.Tasks;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Channel event manager.
    /// Manages channel created, deleted events
    /// </summary>
    public class ChannelEventManager : EventManager
    {
        /// <summary>
        /// Creates new channel event manager
        /// </summary>
        public ChannelEventManager(string eventName, TwinoMQ server)
            : base(server, eventName, null, 0)
        {
        }

        /// <summary>
        /// Triggers channel created or deleted events
        /// </summary>
        public void Trigger(Channel channel, string node = null)
        {
            base.Trigger(new ChannelEvent
                         {
                             Name = channel.Name,
                             Node = node,
                             ActiveClients = channel.ClientsCount(),
                             ClientLimit = channel.Options.ClientLimit
                         });
        }
    }
}