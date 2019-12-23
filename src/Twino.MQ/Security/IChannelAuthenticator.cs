using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Checks if a client can join to the channel.
    /// </summary>
    public interface IChannelAuthenticator
    {
        /// <summary>
        /// Checks if a client can join to the channel.
        /// It should return true if allowed.
        /// </summary>
        Task<bool> Authenticate(Channel channel, MqClient client);
    }
}