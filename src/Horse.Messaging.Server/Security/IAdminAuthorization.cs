using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Security
{
    /// <summary>
    /// Administration authorization implementation
    /// </summary>
    public interface IAdminAuthorization
    {
        /// <summary>
        /// Returns true, if client can update queue options
        /// </summary>
        Task<bool> CanUpdateQueueOptions(MessagingClient client, HorseQueue queue, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can remove the queue
        /// </summary>
        Task<bool> CanRemoveQueue(MessagingClient client, HorseQueue queue);

        /// <summary>
        /// Returns true, if client can clear messages in a queue
        /// </summary>
        Task<bool> CanClearQueueMessages(MessagingClient client, HorseQueue queue, bool priorityMessages, bool messages);

        /// <summary>
        /// Returns true, if client can manage instances
        /// </summary>
        Task<bool> CanManageInstances(MessagingClient client, HorseMessage request);

        /// <summary>
        /// Returns true, if client can receive all connected clients
        /// </summary>
        Task<bool> CanReceiveClients(MessagingClient client);

        /// <summary>
        /// Returns true, if client can receive queue info
        /// </summary>
        Task<bool> CanReceiveQueues(MessagingClient client);

        /// <summary>
        /// Returns true, if client can receive all consumers of queue
        /// </summary>
        Task<bool> CanReceiveQueueConsumers(MessagingClient client, HorseQueue queue);
    }
}