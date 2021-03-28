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
        /// Triggered when a client sends a message to a router
        /// </summary>
        Task OnProduced(MqClient sender, IRouter router, HorseMessage message);
        
        /// <summary>
        /// Triggered when a client sends a message but router cannot be found
        /// </summary>
        Task OnNotFound(MqClient sender, HorseMessage message);
    }
}