using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Network
{
    /// <summary>
    /// Messaging Queue message router implementation by message type
    /// </summary>
    public interface INetworkMessageHandler
    {
        /// <summary>
        /// Handles the received message
        /// </summary>
        Task Handle(MqClient client, HorseMessage message, bool fromNode);
    }
}