using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Core
{
    /// <summary>
    /// Twino TCP Server implementation
    /// </summary>
    public interface ITwinoServer
    {
        /// <summary>
        /// Logger class for Server operations.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Pinger for piped clients that connect and stay alive for a long time
        /// </summary>
        IPinger Pinger { get; }

        /// <summary>
        /// Uses the protocol for new TCP connections that request the protocol
        /// </summary>
        void UseProtocol(ITwinoProtocol protocol);

        /// <summary>
        /// Switches client's protocol to new protocol (finds by name)
        /// </summary>
        Task SwitchProtocol(IConnectionInfo info, string newProtocolName, ConnectionData data);

        /// <summary>
        /// Finds protocol by name
        /// </summary>
        ITwinoProtocol FindProtocol(string name);
    }
}