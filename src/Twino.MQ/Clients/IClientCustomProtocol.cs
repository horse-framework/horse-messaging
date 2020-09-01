using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Clients
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
        void Pong();

        /// <summary>
        /// Sends a TwinoMessage over custom protocol
        /// </summary>
        bool Send(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null);

        /// <summary>
        /// Sends a TwinoMessage over custom protocol
        /// </summary>
        Task<bool> SendAsync(TwinoMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null);
    }
}