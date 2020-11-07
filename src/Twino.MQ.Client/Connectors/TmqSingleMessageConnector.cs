using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Connectors
{
    /// <summary>
    /// Single message connector for TMQ protocol.
    /// </summary>
    public class TmqSingleMessageConnector : SingleMessageConnector<TmqClient, TwinoMessage>
    {
        private readonly MessageObserver _observer;

        /// <summary>
        /// Default TMQ Message reader for connector
        /// </summary>
        public MessageObserver Observer => _observer;

        /// <summary>
        /// If true, automatically subscribes all implemented IQueueConsumer queues
        /// </summary>
        public bool AutoSubscribe { get; set; }

        /// <summary>
        /// Content Serializer for clients in this connector.
        /// If null, default content serializer will be used.
        /// </summary>
        public IMessageContentSerializer ContentSerializer { get; set; }

        /// <summary>
        /// Creates new single message connector for TMQ protocol clients
        /// </summary>
        public TmqSingleMessageConnector(Func<TmqClient> createInstance = null) : base(createInstance)
        {
            _observer = new MessageObserver(ReadMessage);
        }

        private object ReadMessage(TwinoMessage message, Type type)
        {
            if (ContentSerializer == null)
                ContentSerializer = new NewtonsoftContentSerializer();

            return ContentSerializer.Deserialize(message, type);
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
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<TwinoMessage> client, TwinoMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_observer != null)
                _observer.Read((TmqClient) client, payload);
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
        public void On<T>(string queue, Action<T> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(queue, action);
        }

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(Action<T, TwinoMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string queue, ushort content, Action<T, TwinoMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(queue, action);
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
        public void OnDirect<T>(Action<T, TwinoMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T, TwinoMessage> action)
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
        public void Off(string queue)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.Off(queue);
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
        public bool Send(TwinoMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Send(message);

            return false;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult> SendAsync(TwinoMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendAsync(message);

            return Task.FromResult(TwinoResult.Failed());
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoMessage> RequestAsync(TwinoMessage message)
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
                return client.Direct.RequestJson<TResponse>(request);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Direct.RequestJson<TResponse>(target, contentType, request);

            return null;
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> Push(string queue, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.Push(queue, content, waitAcknowledge);

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
        public Task<TwinoResult> PushJson(string queue, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(queue, jsonObject, waitAcknowledge);

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