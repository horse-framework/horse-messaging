using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Direct message event handler implementation (direct and response messages)
    /// </summary>
    public interface IDirectMessageHandler
    {
        /// <summary>
        /// Triggered when a client sends direct message to one or more receivers
        /// </summary>
        Task OnDirect(MessagingClient sender, HorseMessage message, List<MessagingClient> receivers);

        /// <summary>
        /// Triggered when a client sends a direct message but the target cannot be found
        /// </summary>
        Task OnNotFound(MessagingClient sender, HorseMessage message);

        /// <summary>
        /// Triggered when a client sends response message
        /// </summary>
        Task OnResponse(MessagingClient sender, HorseMessage message, MessagingClient receiver);
    }
}