using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Administration authorization implementation
    /// </summary>
    public interface IAdminAuthorization
    {
        /// <summary>
        /// Returns true, if client can update queue options
        /// </summary>
        Task<bool> CanUpdateQueueOptions(MqClient client, TwinoQueue queue, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can remove the queue
        /// </summary>
        Task<bool> CanRemoveQueue(MqClient client, TwinoQueue queue);

        /// <summary>
        /// Returns true, if client can clear messages in a queue
        /// </summary>
        Task<bool> CanClearQueueMessages(MqClient client, TwinoQueue queue, bool priorityMessages, bool messages);

        /// <summary>
        /// Returns true, if client can manage instances
        /// </summary>
        Task<bool> CanManageInstances(MqClient client, TwinoMessage request);

        /// <summary>
        /// Returns true, if client can receive all connected clients
        /// </summary>
        Task<bool> CanReceiveClients(MqClient client);

        /// <summary>
        /// Returns true, if client can receive queue info
        /// </summary>
        Task<bool> CanReceiveQueueInfo(MqClient client, TwinoQueue queue);

        /// <summary>
        /// Returns true, if client can receive all consumers of queue
        /// </summary>
        Task<bool> CanReceiveQueueConsumers(MqClient client, TwinoQueue queue);
    }
}