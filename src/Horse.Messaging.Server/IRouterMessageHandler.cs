using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Direct message event handler implementation (direct and response messages)
    /// </summary>
    public interface IRouterMessageHandler
    {
        /// <summary>
        /// Triggered when a client sends a message but router cannot be found
        /// </summary>
        Task OnRouterNotFound(MessagingClient sender, HorseMessage message);

        /// <summary>
        /// Triggered when a client sends a message, router exists but there are no available bindings
        /// </summary>
        Task OnNotRouted(MessagingClient sender, Router router, HorseMessage message);

        /// <summary>
        /// Triggered when a client sends a message to a router
        /// </summary>
        Task OnRouted(MessagingClient sender, Router router, HorseMessage message);
    }
}