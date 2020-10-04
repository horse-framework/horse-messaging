using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    /// <summary>
    /// Messaging Queue message router implementation by message type
    /// </summary>
    public interface INetworkMessageHandler
    {
        /// <summary>
        /// Handles the received message
        /// </summary>
        Task Handle(MqClient client, TwinoMessage message, bool fromNode);
    }
}