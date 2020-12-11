using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Client;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Security
{
    /// <summary>
    /// Authenticates clients to connect server and receive messages
    /// </summary>
    public interface INodeAuthenticator
    {
        /// <summary>
        /// Checks if the node can connect to the server
        /// It should return true if allowed.
        /// </summary>
        Task<bool> Authenticate(NodeManager server, MqClient client);

        /// <summary>
        /// Checks if the messsage should be sent to the node
        /// </summary>
        Task<bool> CanReceive(HorseClient node, HorseMessage message);
    }
}