using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Routing;
using Horse.Protocols.Hmq;

namespace Horse.Mq
{
    /// <summary>
    /// Direct message event handler implementation (direct and response messages)
    /// </summary>
    public interface IRouterMessageHandler
    {
        /// <summary>
        /// Triggered when a client sends a message but router cannot be found
        /// </summary>
        Task OnRouterNotFound(MqClient sender, HorseMessage message);

        /// <summary>
        /// Triggered when a client sends a message, router exists but there are no available bindings
        /// </summary>
        Task OnNotRouted(MqClient sender, IRouter router, HorseMessage message);

        /// <summary>
        /// Triggered when a client sends a message to a router
        /// </summary>
        Task OnRouted(MqClient sender, IRouter router, HorseMessage message);
    }
}