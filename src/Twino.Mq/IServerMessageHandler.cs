using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    /// <summary>
    /// Implementation for messaging between client and server
    /// </summary>
    public interface IServerMessageHandler
    {
        /// <summary>
        /// when a client sends a message to server
        /// </summary>
        Task Received(MqClient client, TmqMessage message);
    }
}