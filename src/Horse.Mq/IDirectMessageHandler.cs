using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq
{
    /// <summary>
    /// Direct message event handler implementation (direct and response messages)
    /// </summary>
    public interface IDirectMessageHandler
    {
        /// <summary>
        /// Triggered when a client sends direct message to one or more receivers
        /// </summary>
        Task OnDirect(MqClient sender, HorseMessage message, List<MqClient> receivers);

        /// <summary>
        /// Triggered when a client sends a direct message but the target cannot be found
        /// </summary>
        Task OnNotFound(MqClient sender, HorseMessage message);
        
        /// <summary>
        /// Triggered when a client sends response message
        /// </summary>
        Task OnResponse(MqClient sender, HorseMessage message, MqClient receiver);
    }
}