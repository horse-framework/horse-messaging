using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Queues;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Security
{
    /// <summary>
    /// Administration authorization implementation
    /// </summary>
    public interface IAdminAuthorization
    {
        /// <summary>
        /// Returns true, if client can update queue options
        /// </summary>
        Task<bool> CanUpdateQueueOptions(MqClient client, HorseQueue queue, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can remove the queue
        /// </summary>
        Task<bool> CanRemoveQueue(MqClient client, HorseQueue queue);

        /// <summary>
        /// Returns true, if client can clear messages in a queue
        /// </summary>
        Task<bool> CanClearQueueMessages(MqClient client, HorseQueue queue, bool priorityMessages, bool messages);

        /// <summary>
        /// Returns true, if client can manage instances
        /// </summary>
        Task<bool> CanManageInstances(MqClient client, HorseMessage request);

        /// <summary>
        /// Returns true, if client can receive all connected clients
        /// </summary>
        Task<bool> CanReceiveClients(MqClient client);

        /// <summary>
        /// Returns true, if client can receive queue info
        /// </summary>
        Task<bool> CanReceiveQueues(MqClient client);

        /// <summary>
        /// Returns true, if client can receive all consumers of queue
        /// </summary>
        Task<bool> CanReceiveQueueConsumers(MqClient client, HorseQueue queue);
    }
}