using System.Threading.Tasks;
using Horse.Mq.Clients;

namespace Horse.Mq.Security
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
        Task<bool> Authenticate(HorseMq server, MqClient client);
    }
}