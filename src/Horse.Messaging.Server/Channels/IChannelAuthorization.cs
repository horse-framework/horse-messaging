using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// Channel authorization implementation
    /// </summary>
    public interface IChannelAuthorization
    {
        /// <summary>
        /// Returns true if client is allowed to push messages to the channel
        /// </summary>
        Task<bool> CanPush(HorseChannel channel, MessagingClient client, HorseMessage message);
        
        /// <summary>
        /// Returns true if client is allowed to subscribe to the channel
        /// </summary>
        Task<bool> CanSubscribe(HorseChannel channel, MessagingClient client);
    }
}