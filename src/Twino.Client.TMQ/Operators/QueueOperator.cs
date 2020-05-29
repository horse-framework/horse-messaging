using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.Client.TMQ.Operators
{
    /// <summary>
    /// Queue manager object for tmq client
    /// </summary>
    public class QueueOperator
    {
        private readonly TmqClient _client;

        internal QueueOperator(TmqClient client)
        {
            _client = client;
        }

        #region Actions

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public async Task<TwinoResult> Create(string channel, ushort queueId, bool verifyResponse, Action<QueueOptions> optionsAction = null)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateQueue;
            message.SetTarget(channel);
            message.PendingResponse = verifyResponse;

            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);
            message.AddHeader(TmqHeaders.QUEUE_ID, queueId);

            if (optionsAction != null)
            {
                QueueOptions options = new QueueOptions();
                optionsAction(options);

                message.Content = new MemoryStream();
                await JsonSerializer.SerializeAsync(message.Content, options);
            }

            if (verifyResponse)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Finds all queues in channel
        /// </summary>
        public async Task<TmqModelResult<List<QueueInformation>>> List(string channel)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueList;
            message.SetTarget(channel);

            return await _client.SendAndGetJson<List<QueueInformation>>(message);
        }

        /// <summary>
        /// Finds the queue and gets information if exists
        /// </summary>
        public async Task<TmqModelResult<QueueInformation>> GetInfo(string channel, ushort id)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueInformation;
            message.SetTarget(channel);
            message.Content = new MemoryStream(BitConverter.GetBytes(id));

            return await _client.SendAndGetJson<QueueInformation>(message);
        }

        /// <summary>
        /// Deletes a queue in a channel in server.
        /// Required administration permission.
        /// If server has no implementation for administration authorization, request is not allowed.
        /// </summary>
        public async Task<TwinoResult> Delete(string channel, ushort queueId, bool verifyResponse)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveQueue;
            message.SetTarget(channel);
            message.PendingResponse = verifyResponse;
            message.Content = new MemoryStream(BitConverter.GetBytes(queueId));
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Updates queue options
        /// </summary>
        public async Task<TwinoResult> SetOptions(string channel, ushort queueId, Action<QueueOptions> optionsAction)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.UpdateQueue;
            message.SetTarget(channel);
            message.PendingResponse = true;
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);
            message.AddHeader(TmqHeaders.QUEUE_ID, queueId);

            QueueOptions options = new QueueOptions();
            optionsAction(options);

            message.Content = new MemoryStream();
            await JsonSerializer.SerializeAsync(message.Content, options);

            return await _client.WaitResponse(message, true);
        }

        /// <summary>
        /// Clears messages in a queue.
        /// Required administration permission.
        /// If server has no implementation for administration authorization, request is not allowed.
        /// </summary>
        public Task<TwinoResult> ClearMessages(string channel, ushort queueId, bool clearPriorityMessages, bool clearMessages)
        {
            if (!clearPriorityMessages && !clearMessages)
                return Task.FromResult(TwinoResult.Failed());

            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ClearMessages;
            message.SetTarget(channel);
            message.PendingResponse = true;
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);
            message.AddHeader(TmqHeaders.QUEUE_ID, queueId);

            if (clearPriorityMessages)
                message.AddHeader(TmqHeaders.PRIORITY_MESSAGES, "yes");

            if (clearMessages)
                message.AddHeader(TmqHeaders.MESSAGES, "yes");

            return _client.WaitResponse(message, true);
        }

        #endregion

        #region Events

        /// <summary> 
        /// Triggers the action when a message is produced to a queue in a channel
        /// </summary>
        public async Task<bool> OnMessageProduced(string channel, ushort queue, Action<MessageEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all message produced events in a queue in a channel 
        /// </summary>
        public async Task<bool> OffMessageProduced(string channel, ushort queue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Triggers the action when a queue is created in a channel
        /// </summary>
        public async Task<bool> OnCreated(string channel, Action<QueueEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all queue created events in a channel 
        /// </summary>
        public async Task<bool> OffCreated(string channel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Triggers the action when a queue is updated in a channel
        /// </summary>
        public async Task<bool> OnUpdated(string channel, Action<QueueEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all queue updated events in a channel 
        /// </summary>
        public async Task<bool> OffUpdated(string channel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Triggers the action when a queue is deleted in a channel
        /// </summary>
        public async Task<bool> OnDeleted(string channel, Action<QueueEvent> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Unsubscribes from all queue deleted events in a channel 
        /// </summary>
        public async Task<bool> OffDeleted(string channel)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}