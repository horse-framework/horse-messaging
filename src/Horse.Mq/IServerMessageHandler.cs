using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Protocols.Hmq;

namespace Horse.Mq
{
    /// <summary>
    /// Implementation for messaging between client and server
    /// </summary>
    public interface IServerMessageHandler
    {
        /// <summary>
        /// when a client sends a message to server
        /// </summary>
        Task Received(MqClient client, HorseMessage message);
    }
}