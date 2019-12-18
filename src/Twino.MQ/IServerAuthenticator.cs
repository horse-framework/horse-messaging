using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    public interface IServerAuthenticator
    {
        /// <summary>
        /// Checks if the instance can connect to the server
        /// It should return true if allowed.
        /// </summary>
        Task<bool> Authenticate(MqServer server, MqClient client);

        /// <summary>
        /// Checks if the messsage should be sent to the instance
        /// </summary>
        Task<bool> CanReceive(TmqClient instanceClient, TmqMessage message);
    }
}