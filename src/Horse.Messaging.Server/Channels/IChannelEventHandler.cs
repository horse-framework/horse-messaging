using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// HorseChannel event handler
    /// </summary>
    public interface IChannelEventHandler
    {
        /// <summary>
        /// Triggered when a new channel is creates
        /// </summary>
        Task OnCreated(HorseChannel channel);

        /// <summary>
        /// Triggered when a channel is removed
        /// </summary>
        Task OnRemoved(HorseChannel channel);

        /// <summary>
        /// Triggered when a channel message is published
        /// </summary>
        Task OnPublish(HorseChannel channel, HorseMessage message);
        
        /// <summary>
        /// Triggered when a client is subscribed to a channel
        /// </summary>
        Task OnSubscribe(HorseChannel channel, MessagingClient client);

        /// <summary>
        /// When a client is unsubscribed from a channel
        /// </summary>
        Task OnUnsubscribe(HorseChannel channel, MessagingClient client);
    }
}