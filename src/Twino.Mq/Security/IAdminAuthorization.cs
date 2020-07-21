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
        /// Returns true, if user can remove the channel
        /// </summary>
        Task<bool> CanRemoveChannel(MqClient client, MqServer server, Channel channel);

        /// <summary>
        /// Returns true, if client can update queue options
        /// </summary>
        Task<bool> CanUpdateQueueOptions(MqClient client, Channel channel, ChannelQueue queue, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can remove the queue in a channel
        /// </summary>
        Task<bool> CanRemoveQueue(MqClient client, ChannelQueue queue);

        /// <summary>
        /// Returns true, if client can clear messages in a queue
        /// </summary>
        Task<bool> CanClearQueueMessages(MqClient client, ChannelQueue queue, bool priorityMessages, bool messages);

        /// <summary>
        /// Returns true, if client can manage instances
        /// </summary>
        Task<bool> CanManageInstances(MqClient client, TmqMessage request);

        /// <summary>
        /// Returns true, if client can receive all connected clients
        /// </summary>
        Task<bool> CanReceiveClients(MqClient client);

        /// <summary>
        /// Returns true, if client can receive channel info
        /// </summary>
        Task<bool> CanReceiveChannelInfo(MqClient client, Channel channel);

        /// <summary>
        /// Returns true, if client can receive all consumers of channel
        /// </summary>
        Task<bool> CanReceiveChannelConsumers(MqClient client, Channel channel);

        /// <summary>
        /// Returns true, if client can receive all queues of channel
        /// </summary>
        Task<bool> CanReceiveChannelQueues(MqClient client, Channel channel);
    }
}