using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;

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

        //todo: check
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

        //todo: check
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

        //todo: check
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
        public Task<TwinoResult> SetOptions(string channel, ushort queueId, bool clearPriorityMessages, bool clearMessages)
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
    }
}