using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;

namespace Horse.Mq.Security
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
        Task<bool> Authenticate(HorseQueue queue, MqClient client);
    }
}