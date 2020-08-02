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
        private readonly MessageObserver _observer;

        /// <summary>
        /// Default TMQ Message reader for connector
        /// </summary>
        public MessageObserver Observer => _observer;

        /// <summary>
        /// If true, automatically joins all subscribed channels
        /// </summary>
        public bool AutoJoinConsumerChannels { get; set; }

        /// <summary>
        /// If true, disconnected from server when auto join fails
        /// </summary>
        public bool DisconnectionOnAutoJoinFailure { get; set; } = true;

        /// <summary>
        /// Content Serializer for clients in this connector.
        /// If null, default content serializer will be used.
        /// </summary>
        public IMessageContentSerializer ContentSerializer { get; set; }

        /// <summary>
        /// Creates new sticky connector for TMQ protocol clients
        /// </summary>
        public TmqStickyConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
            _observer = new MessageObserver(ReadMessage);
        }

        private object ReadMessage(TmqMessage message, Type type)
        {
            if (ContentSerializer == null)
                ContentSerializer = new NewtonsoftContentSerializer();
            
            return ContentSerializer.Deserialize(message, type);
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_observer != null)
                _observer.Read((TmqClient) client, payload);
        }

        /// <inheritdoc />
        protected override void ClientConnected(SocketBase client)
        {
            if (ContentSerializer != null)
            {
                if (client is TmqClient tmqClient)
                    tmqClient.JsonSerializer = ContentSerializer;
            }

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
            if (_observer == null)
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

            string[] channels = _observer.GetSubscribedChannels();
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
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(channel, content, action);
        }

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(channel, content, action);
        }

        #endregion

        #region OnDirect - Consume

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(content, action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T, TmqMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(content, action);
        }

        #endregion

        #region Off

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off<T>()
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.Off<T>();
        }

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off(string channel, ushort content)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.Off(channel, content);
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect<T>()
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OffDirect<T>();
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect(ushort content)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OffDirect(content);
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
        public Task<TwinoResult> SendDirectJsonAsync<T>(T model, bool waitForAcknowledge = false)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendJsonAsync(MessageType.DirectMessage, model, waitForAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendDirectJsonAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge = false)
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
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.RequestJson<TResponse>(request);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.RequestJson<TResponse>(target, contentType, request);

            return null;
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> Push(string channel, ushort queueId, MemoryStream content, bool waitAcknowledge = false)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.Push(channel, queueId, content, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(object jsonObject, bool waitAcknowledge = false)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(string channel, ushort queueId, object jsonObject, bool waitAcknowledge = false)
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
        public Task<TwinoResult> Publish(string routerName, MemoryStream content, bool waitAcknowledge = false)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.Publish(routerName, content.ToArray(), waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> PublishJson(object jsonObject, bool waitAcknowledge = false)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<TwinoResult> PublishJson(string routerName, object jsonObject, bool waitAcknowledge = false)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(routerName, jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Publish a string message to a router and waits for a response message
        /// </summary>
        public Task<TmqMessage> PublishRequest(string routerName, string message, ushort contentType = 0)
        {
            TmqClient client = GetClient();
            return client.Routers.PublishRequest(routerName, message, contentType);
        }

        /// <summary>
        /// Publish a JSON message to a router and waits for a response message
        /// </summary>
        public Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request)
        {
            TmqClient client = GetClient();
            return client.Routers.PublishRequestJson<TRequest, TResponse>(request);
        }

        /// <summary>
        /// Publish a JSON message to a router and waits for a response message
        /// </summary>
        public Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType = null)
        {
            TmqClient client = GetClient();
            return client.Routers.PublishRequestJson<TRequest, TResponse>(routerName, request, contentType);
        }

        #endregion
    }
}