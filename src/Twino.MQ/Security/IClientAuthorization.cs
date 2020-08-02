using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Security
{
    /// <summary>
    /// Implementation of the object that checks authority of client operations
    /// </summary>
    public interface IClientAuthorization
    {
        /// <summary>
        /// Returns true, if user can create the channel
        /// </summary>
        Task<bool> CanCreateChannel(MqClient client, TwinoMQ server, string channelName);

        /// <summary>
        /// Returns true, if client can create new queue in the channel
        /// </summary>
        Task<bool> CanCreateQueue(MqClient client, Channel channel, ushort contentType, NetworkOptionsBuilder options);

        /// <summary>
        /// Returns true, if client can send a peer message
        /// </summary>
        Task<bool> CanDirectMessage(MqClient sender, TmqMessage message, MqClient receiver);

        /// <summary>
        /// Returns true, if client can send a peer message
        /// </summary>
        Task<bool> CanResponseMessage(MqClient sender, TmqMessage message, MqClient receiver);

        /// <summary>
        /// Returns true, if client can send a message to the queue
        /// </summary>
        Task<bool> CanMessageToQueue(MqClient client, ChannelQueue queue, TmqMessage message);

        /// <summary>
        /// Returns true, if client can pull a message from the queue
        /// </summary>
        Task<bool> CanPullFromQueue(ChannelClient client, ChannelQueue queue);
        
        /// <summary>
        /// Returns true, if client can subscribe to the event
        /// </summary>
        bool CanSubscribeEvent(MqClient client, string eventName, string channelName, ushort queueId);
    }
}