using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Client.TMQ.Exceptions;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Sticky connector for TMQ protocol.
    /// </summary>
    public class TmqStickyConnector : StickyConnector<TmqClient, TmqMessage>, ITwinoBus
    {
        private MessageConsumer _consumer;

        /// <summary>
        /// Default TMQ Message reader for connector
        /// </summary>
        public MessageConsumer Consumer => _consumer;

        /// <summary>
        /// If true, automatically joins all subscribed channels
        /// </summary>
        public bool AutoJoinConsumerChannels { get; set; }

        /// <summary>
        /// If true, disconnected from server when auto join fails
        /// </summary>
        public bool DisconnectionOnAutoJoinFailure { get; set; } = true;

        /// <summary>
        /// Creates new sticky connector for TMQ protocol clients
        /// </summary>
        public TmqStickyConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
        }

        /// <summary>
        /// Inits the JSON reader on the connector
        /// </summary>
        public void InitJsonReader()
        {
            _consumer = MessageConsumer.JsonConsumer();
        }

        /// <summary>
        /// Inits a custom reader on the connector
        /// </summary>
        public void InitReader(Func<TmqMessage, Type, object> serailizationAction)
        {
            _consumer = new MessageConsumer(serailizationAction);
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_consumer != null)
                _consumer.Read((TmqClient) client, payload);
        }

        /// <inheritdoc />
        protected override void ClientConnected(SocketBase client)
        {
            base.ClientConnected(client);

            if (AutoJoinConsumerChannels)
                _ = JoinAllSubscribedChannels(true, DisconnectionOnAutoJoinFailure);
        }

        /// <summary>
        /// Joins all subscribed channels
        /// </summary>
        /// <param name="verify">If true, waits response from server for each join operation</param>
        /// <param name="disconnectOnFail">If any of channels fails to join, disconnected from server</param>
        /// <param name="silent">If true, errors are hidden, no exception thrown</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">Thrown if there is no consumer initialized</exception>
        /// <exception cref="TwinoSocketException">Thrown if not connected to server</exception>
        public async Task<bool> JoinAllSubscribedChannels(bool verify, bool disconnectOnFail = true, bool silent = true)
        {
            if (_consumer == null)
            {
                if (silent)
                    return false;

                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");
            }

            TmqClient client = GetClient();
            if (client == null)
            {
                if (silent)
                    return false;

                throw new TwinoSocketException("There is no active connection");
            }

            string[] channels = _consumer.GetSubscribedChannels();
            foreach (string channel in channels)
            {
                TwinoResult joinResult = await client.Channels.Join(channel, verify);
                if (joinResult.Code == TwinoResultCode.Ok)
                    continue;

                if (disconnectOnFail)
                    client.Disconnect();

                if (!silent)
                    throw new TwinoChannelException($"Can't join to {channel} channel: {joinResult.Reason} ({joinResult.Code})");

                return false;
            }

            return true;
        }

        #region On - Consume

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(Action<T> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.On(channel, content, action);
        }

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(Action<T, TmqMessage> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T, TmqMessage> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.On(channel, content, action);
        }

        #endregion

        #region OnDirect - Consume

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(Action<T> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.OnDirect(content, action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(Action<T, TmqMessage> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T, TmqMessage> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.OnDirect(content, action);
        }

        #endregion

        #region Off

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off<T>()
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.Off<T>();
        }

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off(string channel, ushort content)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.Off(channel, content);
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect<T>()
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.OffDirect<T>();
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect(ushort content)
        {
            if (_consumer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _consumer.OffDirect(content);
        }

        #endregion

        #region Send

        /// <summary>
        /// Sends a message
        /// </summary>
        public bool Send(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Send(message);

            return false;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendAsync(message);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendDirectJsonAsync<T>(T model, bool waitForAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendJsonAsync(MessageType.DirectMessage, model, waitForAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendDirectJsonAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendJsonAsync(MessageType.DirectMessage, target, contentType, model, waitForAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TmqMessage> RequestAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Request(message);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TmqMessage> RequestJsonAsync<TRequest>(TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.RequestJson(request);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TmqMessage> RequestJsonAsync<TRequest>(string target, ushort contentType, TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.RequestJson(target, contentType, request);

            return null;
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> Push(string channel, ushort queueId, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.Push(channel, queueId, content, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(string channel, ushort queueId, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(channel, queueId, jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion

        #region Publish

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> Publish(string routerName, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.Publish(routerName, content.ToArray(), waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> PublishJson(object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> PublishJson(string routerName, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(routerName, jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion
    }
}