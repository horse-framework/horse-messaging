using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Checks if a client can connect to the server
    /// </summary>
    public interface IClientAuthenticator
    {
        /// <summary>
        /// Checks if a client can connect to the server
        /// It should return true if allowed.
        /// </summary>
        Task<bool> Authenticate(MQServer server, MqClient client);
    }
}