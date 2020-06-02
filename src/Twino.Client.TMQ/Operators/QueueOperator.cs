using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Twino.Client.TMQ.Internal;
using Twino.Client.TMQ.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;
using Twino.Protocols.TMQ.Models.Events;

namespace Twino.Client.TMQ.Operators
{
    /// <summary>
    /// Queue manager object for tmq client
    /// </summary>
    public class QueueOperator : IDisposable
    {
        private readonly TmqClient _client;

        internal Dictionary<string, PullContainer> PullContainers { get; }
        private Timer _pullContainerTimeoutHandler;

        internal QueueOperator(TmqClient client)
        {
            _client = client;
            PullContainers = new Dictionary<string, PullContainer>();
            _pullContainerTimeoutHandler = new Timer(HandleTimeoutPulls, null, 1000, 1000);
        }

        /// <summary>
        /// Handles timed out pull requests and removed them
        /// </summary>
        private void HandleTimeoutPulls(object state)
        {
            try
            {
                if (PullContainers.Count > 0)
                {
                    List<PullContainer> timedouts = new List<PullContainer>();
                    lock (PullContainers)
                    {
                        foreach (PullContainer container in PullContainers.Values)
                        {
                            if (container.Status == PullProcess.Receiving && container.LastReceived + _client.PullTimeout < DateTime.UtcNow)
                                timedouts.Add(container);
                        }
                    }

                    foreach (PullContainer container in timedouts)
                    {
                        lock (PullContainers)
                            PullContainers.Remove(container.RequestId);

                        container.Complete(null);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        public void Dispose()
        {
            _pullContainerTimeoutHandler?.Dispose();
        }

        #region Actions

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public async Task<TwinoResult> Create(string channel, ushort queueId, Action<QueueOptions> optionsAction = null)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateQueue;
            message.SetTarget(channel);
            message.PendingResponse = true;

            message.AddHeader(TmqHeaders.CHANNEL_NAME, channel);
            message.AddHeader(TmqHeaders.QUEUE_ID, queueId);

            if (optionsAction != null)
            {
                QueueOptions options = new QueueOptions();
                optionsAction(options);

                message.Content = new MemoryStream();
                await JsonSerializer.SerializeAsync(message.Content, options);
            }

            message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, true);
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
        /// Removes a queue in a channel in server.
        /// Required administration permission.
        /// If server has no implementation for administration authorization, request is not allowed.
        /// </summary>
        public async Task<TwinoResult> Remove(string channel, ushort queueId)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveQueue;
            message.SetTarget(channel);
            message.PendingResponse = true;
            message.Content = new MemoryStream(BitConverter.GetBytes(queueId));
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, true);
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

        #region Push - Pull

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<TwinoResult> PushJson(string channel, ushort queueId, object jsonObject, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage(MessageType.QueueMessage, channel, queueId);
            message.Content = new MemoryStream();
            message.PendingAcknowledge = waitAcknowledge;
            await JsonSerializer.SerializeAsync(message.Content, jsonObject, jsonObject.GetType());

            if (waitAcknowledge)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.SendAndWaitForAcknowledge(message, waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<TwinoResult> Push(string channel, ushort queueId, string content, bool waitAcknowledge)
        {
            return await Push(channel, queueId, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<TwinoResult> Push(string channel, ushort queueId, MemoryStream content, bool waitAcknowledge)
        {
            TmqMessage message = new TmqMessage(MessageType.QueueMessage, channel, queueId);
            message.Content = content;
            message.PendingAcknowledge = waitAcknowledge;

            if (waitAcknowledge)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.SendAndWaitForAcknowledge(message, waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue and does not wait for acknowledge.
        /// Uses legacy callback method instead of async
        /// </summary>
        public bool PushJsonSync(string channel, ushort queueId, object jsonObject)
        {
            TmqMessage message = new TmqMessage(MessageType.QueueMessage, channel, queueId);
            message.PendingAcknowledge = false;
            byte[] data = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(jsonObject, jsonObject.GetType());
            message.Content = new MemoryStream(data);
            message.Content.Position = 0;

            if (_client.UseUniqueMessageId)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return _client.Send(message);
        }

        /// <summary>
        /// Pushes a message to a queue and does not wait for acknowledge.
        /// Uses legacy callback method instead of async
        /// </summary>
        public bool PushSync(string channel, ushort queueId, byte[] data)
        {
            TmqMessage message = new TmqMessage(MessageType.QueueMessage, channel, queueId);
            message.PendingAcknowledge = false;
            message.Content = new MemoryStream(data);
            message.Content.Position = 0;

            if (_client.UseUniqueMessageId)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return _client.Send(message);
        }

        /// <summary>
        /// Request a message from Pull queue
        /// </summary>
        public async Task<PullContainer> Pull(PullRequest request, Func<int, TmqMessage, Task> actionForEachMessage = null)
        {
            TmqMessage message = new TmqMessage(MessageType.QueuePullRequest, request.Channel, request.QueueId);
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            message.AddHeader(TmqHeaders.COUNT, request.Count);

            if (request.ClearAfter == ClearDecision.AllMessages)
                message.AddHeader(TmqHeaders.CLEAR, "all");
            else if (request.ClearAfter == ClearDecision.PriorityMessages)
                message.AddHeader(TmqHeaders.CLEAR, "High-Priority");
            else if (request.ClearAfter == ClearDecision.Messages)
                message.AddHeader(TmqHeaders.CLEAR, "Default-Priority");

            if (request.GetQueueMessageCounts)
                message.AddHeader(TmqHeaders.INFO, "yes");

            if (request.Order == MessageOrder.LIFO)
                message.AddHeader(TmqHeaders.ORDER, TmqHeaders.LIFO);

            PullContainer container = new PullContainer(message.MessageId, request.Count, actionForEachMessage);
            lock (PullContainers)
                PullContainers.Add(message.MessageId, container);

            TwinoResult sent = await _client.SendAsync(message);
            if (sent.Code != TwinoResultCode.Ok)
            {
                lock (PullContainers)
                    PullContainers.Remove(message.MessageId);

                container.Complete("Error");
            }

            return await container.GetAwaitableTask();
        }

        #endregion

        #region Events

        /// <summary> 
        /// Triggers the action when a message is produced to a queue in a channel
        /// </summary>
        public async Task<bool> OnMessageProduced(string channel, ushort queue, Action<MessageEvent> action)
        {
            bool ok = await _client.EventSubscription(EventNames.MessageProduced, true, channel, queue);
            if (ok)
                _client.Events.Add(EventNames.MessageProduced, channel, queue, action, typeof(MessageEvent));

            return ok;
        }

        /// <summary>
        /// Unsubscribes from all message produced events in a queue in a channel 
        /// </summary>
        public async Task<bool> OffMessageProduced(string channel, ushort queue)
        {
            bool ok = await _client.EventSubscription(EventNames.MessageProduced, false, channel, queue);
            if (ok)
                _client.Events.Remove(EventNames.MessageProduced, channel, queue);

            return ok;
        }

        /// <summary>
        /// Triggers the action when a queue is created in a channel
        /// </summary>
        public async Task<bool> OnCreated(string channel, Action<QueueEvent> action)
        {
            bool ok = await _client.EventSubscription(EventNames.QueueCreated, true, channel, null);
            if (ok)
                _client.Events.Add(EventNames.QueueCreated, channel, 0, action, typeof(QueueEvent));

            return ok;
        }

        /// <summary>
        /// Unsubscribes from all queue created events in a channel 
        /// </summary>
        public async Task<bool> OffCreated(string channel)
        {
            bool ok = await _client.EventSubscription(EventNames.QueueCreated, false, channel, null);
            if (ok)
                _client.Events.Remove(EventNames.QueueCreated, channel, 0);

            return ok;
        }

        /// <summary>
        /// Triggers the action when a queue is updated in a channel
        /// </summary>
        public async Task<bool> OnUpdated(string channel, Action<QueueEvent> action)
        {
            bool ok = await _client.EventSubscription(EventNames.QueueUpdated, true, channel, null);
            if (ok)
                _client.Events.Add(EventNames.QueueUpdated, channel, 0, action, typeof(QueueEvent));

            return ok;
        }

        /// <summary>
        /// Unsubscribes from all queue updated events in a channel 
        /// </summary>
        public async Task<bool> OffUpdated(string channel)
        {
            bool ok = await _client.EventSubscription(EventNames.QueueUpdated, false, channel, null);
            if (ok)
                _client.Events.Remove(EventNames.QueueUpdated, channel, 0);

            return ok;
        }

        /// <summary>
        /// Triggers the action when a queue is removed in a channel
        /// </summary>
        public async Task<bool> OnRemoved(string channel, Action<QueueEvent> action)
        {
            bool ok = await _client.EventSubscription(EventNames.QueueRemoved, true, channel, null);
            if (ok)
                _client.Events.Add(EventNames.QueueRemoved, channel, 0, action, typeof(QueueEvent));

            return ok;
        }

        /// <summary>
        /// Unsubscribes from all queue removed events in a channel 
        /// </summary>
        public async Task<bool> OffRemoved(string channel)
        {
            bool ok = await _client.EventSubscription(EventNames.QueueRemoved, false, channel, null);
            if (ok)
                _client.Events.Remove(EventNames.QueueRemoved, channel, 0);

            return ok;
        }

        #endregion
    }
}