using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Checks if a client can subscribe to the queue.
    /// </summary>
    public interface IQueueAuthenticator
    {
        /// <summary>
        /// Checks if a client can subscribe to the queue or get information about the queue.
        /// It should return true if allowed.
        /// </summary>
        Task<bool> Authenticate(TwinoQueue queue, MqClient client);
    }
}