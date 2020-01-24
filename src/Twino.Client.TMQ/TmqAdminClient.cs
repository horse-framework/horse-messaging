using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Administrator TMQ Client class.
    /// Includes instance, channel, queue and client management methods
    /// </summary>
    public class TmqAdminClient : TmqClient
    {
        #region Instance

        /// <summary>
        /// Gets all instances connected to server
        /// </summary>
        public async Task<TmqResult<List<InstanceInformation>>> GetInstances()
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.InstanceList;

            return await SendAndGetJson<List<InstanceInformation>>(message);
        }

        #endregion

        #region Client

        /// <summary>
        /// Gets all consumers of channel
        /// </summary>
        public async Task<TmqResult<List<ClientInformation>>> GetConnectedClients()
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ClientList;

            return await SendAndGetJson<List<ClientInformation>>(message);
        }

        #endregion

        #region Channel

        /// <summary>
        /// Finds the channel and gets information if exists
        /// </summary>
        public async Task<TmqResult<ChannelInformation>> GetChannelInfo(string name)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelInformation;
            message.SetTarget(name);

            return await SendAndGetJson<ChannelInformation>(message);
        }

        /// <summary>
        /// Gets all channels in server
        /// </summary>
        public async Task<TmqResult<List<ChannelInformation>>> GetChannels()
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelList;

            return await SendAndGetJson<List<ChannelInformation>>(message);
        }

        /// <summary>
        /// Gets all consumers of channel
        /// </summary>
        public async Task<TmqResult<List<ClientInformation>>> GetConsumers(string channel)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelConsumers;

            return await SendAndGetJson<List<ClientInformation>>(message);
        }

        /// <summary>
        /// Removes a channel and all queues in it
        /// </summary>
        public async Task<TmqResponseCode> RemoveChannel(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveChannel;
            message.SetTarget(channel);
            message.ResponseRequired = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitResponse(message, verifyResponse);
        }

        #endregion

        #region Queue

        /// <summary>
        /// Finds all queues in channel
        /// </summary>
        public async Task<TmqResult<List<QueueInformation>>> GetQueues(string channel)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueList;
            message.SetTarget(channel);

            return await SendAndGetJson<List<QueueInformation>>(message);
        }

        /// <summary>
        /// Finds the queue and gets information if exists
        /// </summary>
        public async Task<TmqResult<QueueInformation>> GetQueueInfo(string channel, ushort id)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueInformation;
            message.SetTarget(channel);
            message.Content = new MemoryStream(BitConverter.GetBytes(id));

            return await SendAndGetJson<QueueInformation>(message);
        }

        /// <summary>
        /// Removes a queue in a channel in server
        /// </summary>
        public async Task<TmqResponseCode> RemoveQueue(string channel, ushort queueId, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveQueue;
            message.SetTarget(channel);
            message.ResponseRequired = verifyResponse;
            message.Content = new MemoryStream(BitConverter.GetBytes(queueId));

            if (verifyResponse)
                message.SetMessageId(UniqueIdGenerator.Create());

            return await WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Updates queue options
        /// </summary>
        public async Task<TmqResponseCode> SetQueueOptions(string channel, ushort queueId, Action<QueueOptions> optionsAction)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.UpdateQueue;
            message.SetTarget(channel);
            message.ResponseRequired = true;
            message.SetMessageId(UniqueIdGenerator.Create());

            QueueOptions options = new QueueOptions();
            optionsAction(options);
            message.Content = new MemoryStream(Encoding.UTF8.GetBytes(options.Serialize(queueId)));

            return await WaitResponse(message, true);
        }

        #endregion
    }
}