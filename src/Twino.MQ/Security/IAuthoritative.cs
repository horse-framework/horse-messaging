using System.Threading.Tasks;
using Twino.MQ.Channels;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Implementation of the object that checks authority of client operations
    /// </summary>
    public interface IAuthoritative
    {
        /// <summary>
        /// Returns true, if client is allowed to join to the channel
        /// </summary>
        Task<bool> CanJoin(MqClient client, Channel channel);

        /// <summary>
        /// Returns true, if user can client the channel
        /// </summary>
        Task<bool> CanCreateChannel(MqClient client, MQServer server, string channelName);

        /// <summary>
        /// Returns true, if client can create new queue in the channel
        /// </summary>
        Task<bool> CanCreateQueue(MqClient client, Channel channel, ushort contentType);

        /// <summary>
        /// Returns true, if client can send a peer message
        /// </summary>
        Task<bool> CanMessageToPeer(MqClient client, TmqMessage message);

        /// <summary>
        /// Returns true, if client can send a message to the queue
        /// </summary>
        Task<bool> CanMessageToQueue(MqClient client, ChannelQueue queue, TmqMessage message);
    }
}