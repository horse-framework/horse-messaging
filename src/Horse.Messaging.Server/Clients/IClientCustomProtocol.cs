using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Clients
{
    /// <summary>
    /// Custom protocol for MQ Clients.
    /// Protocol implementation, If a client uses different protocol. 
    /// </summary>
    public interface IClientCustomProtocol
    {
        /// <summary>
        /// Sends PING over custom protocol
        /// </summary>
        void Ping();

        /// <summary>
        /// Sends PONG over custom protocol
        /// </summary>
        void Pong(object pingMessage = null);

        /// <summary>
        /// Sends a HorseMessage over custom protocol
        /// </summary>
        bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null);

        /// <summary>
        /// Sends a HorseMessage over custom protocol
        /// </summary>
        Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null);
    }
}