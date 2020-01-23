using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Administrator TMQ Client class.
    /// Includes instance, channel, queue and client management methods
    /// </summary>
    public class TmqAdministratorClient : TmqClient
    {
        #region Instance

        /// <summary>
        /// Gets all instances connected to server
        /// </summary>
        public async Task<List<InstanceInformation>> GetInstances()
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.InstanceList;

            return await SendAndGetJson<List<InstanceInformation>>(message);
        }

        #endregion
        
        #region Client
        
        //todo: get clients
        //todo: get channel clients
        
        #endregion

        #region Channel

        /// <summary>
        /// Finds the channel and gets information if exists
        /// </summary>
        public async Task<ChannelInformation> GetChannelInfo(string name)
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
        public async Task<List<ChannelInformation>> GetChannels()
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelList;

            return await SendAndGetJson<List<ChannelInformation>>(message);
        }

        #endregion

        #region Queue

        /// <summary>
        /// Finds all queues in channel
        /// </summary>
        public async Task<List<QueueInformation>> GetQueues(string channel)
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
        public async Task<QueueInformation> GetQueueInfo(string channel, ushort id)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueInformation;
            message.SetTarget(channel);
            message.Content = new MemoryStream(BitConverter.GetBytes(id));

            return await SendAndGetJson<QueueInformation>(message);
        }

        #endregion
    }
}