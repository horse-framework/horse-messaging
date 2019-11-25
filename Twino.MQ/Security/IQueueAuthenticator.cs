using System.Threading.Tasks;
using Twino.MQ.Channels;
using Twino.MQ.Clients;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Checks if a client can subscribe to the queue.
    /// </summary>
    public interface IQueueAuthenticator
    {
        /// <summary>
        /// Checks if a client can subscribe to the queue.
        /// It should return true if allowed.
        /// </summary>
        Task<bool> Authenticate(ChannelQueue channel, MqClient client, ClientInformation information);
    }
}