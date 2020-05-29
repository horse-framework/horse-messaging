using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.Client.TMQ.Operators
{
    /// <summary>
    /// Channel manager object for tmq client
    /// </summary>
    public class ChannelOperator
    {
        private readonly TmqClient _client;

        internal ChannelOperator(TmqClient client)
        {
            _client = client;
        }

        #region Join - Leave

        /// <summary>
        /// Joins to a channel
        /// </summary>
        public async Task<TwinoResult> Join(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Join;
            message.SetTarget(channel);
            message.PendingResponse = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Leaves from a channel
        /// </summary>
        public async Task<TwinoResult> Leave(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Leave;
            message.SetTarget(channel);
            message.PendingResponse = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, verifyResponse);
        }

        #endregion

        #region Create - Delete

        /// <summary>
        /// Creates a new channel without any queue
        /// </summary>
        public async Task<TwinoResult> Create(string channel, bool verifyResponse, Action<ChannelOptions> optionsAction = null)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateChannel;
            message.SetTarget(channel);
            message.PendingResponse = verifyResponse;
            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            if (optionsAction != null)
            {
                ChannelOptions options = new ChannelOptions();
                optionsAction(options);
                message.Content = new MemoryStream();
                await System.Text.Json.JsonSerializer.SerializeAsync(message.Content, options);
            }

            return await _client.WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Deletes a channel and all queues in it
        /// </summary>
        public async Task<TwinoResult> Delete(string channel, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveChannel;
            message.SetTarget(channel);
            message.PendingResponse = verifyResponse;
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);

            return await _client.WaitResponse(message, verifyResponse);
        }

        #endregion

        #region Get

        /// <summary>
        /// Finds the channel and gets information if exists
        /// </summary>
        public async Task<TmqModelResult<ChannelInformation>> GetInfo(string name)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelInformation;
            message.SetTarget(name);
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.AddHeader(TmqHeaders.CHANNEL_NAME, name);

            return await _client.SendAndGetJson<ChannelInformation>(message);
        }

        /// <summary>
        /// Gets all channels in server.
        /// Filter supports * joker character.
        /// </summary>
        public async Task<TmqModelResult<List<ChannelInformation>>> List(string filter = null)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelList;
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            if (!string.IsNullOrEmpty(filter))
                message.AddHeader(TmqHeaders.CHANNEL_NAME, filter);

            return await _client.SendAndGetJson<List<ChannelInformation>>(message);
        }

        /// <summary>
        /// Gets all consumers of channel
        /// </summary>
        public async Task<TmqModelResult<List<ClientInformation>>> GetConsumers(string channel)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.SetTarget(channel);
            message.ContentType = KnownContentTypes.ChannelConsumers;
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);

            return await _client.SendAndGetJson<List<ClientInformation>>(message);
        }

        #endregion

        #region Subscription Events

        public async Task<bool> OnClientJoined(string channelName, Action<SubscriptionEvent> action)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> OffClientJoined(string channelName)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> OnClientLeft(string channelName, Action<SubscriptionEvent> action)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> OffClientLeft(string channelName)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Channel Events

        /// <summary> 
        /// Triggers the action when a client is created in the server
        /// </summary>
        public async Task<bool> OnCreated(Action<ChannelEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all channel created events
        /// </summary>
        public async Task<bool> OffCreated()
        {
            throw new NotImplementedException();
        }

        /// <summary> 
        /// Triggers the action when a client is updated in the server
        /// </summary>
        public async Task<bool> OnUpdated(Action<ChannelEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all channel updated events
        /// </summary>
        public async Task<bool> OffUpdated()
        {
            throw new NotImplementedException();
        }

        /// <summary> 
        /// Triggers the action when a client is deleted in the server
        /// </summary>
        public async Task<bool> OnDeleted(Action<ChannelEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all channel deleted events
        /// </summary>
        public async Task<bool> OffDeleted()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}