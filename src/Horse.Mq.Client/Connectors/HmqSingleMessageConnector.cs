using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Client.Connectors;
using Horse.Core;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Connectors
{
    /// <summary>
    /// Single message connector for HMQ protocol.
    /// </summary>
    public class HmqSingleMessageConnector : SingleMessageConnector<HorseClient, HorseMessage>
    {
        private readonly MessageObserver _observer;

        /// <summary>
        /// Default HMQ Message reader for connector
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
        /// Creates new single message connector for HMQ protocol clients
        /// </summary>
        public HmqSingleMessageConnector(Func<HorseClient> createInstance = null) : base(createInstance)
        {
            _observer = new MessageObserver(ReadMessage);
        }

        private object ReadMessage(HorseMessage message, Type type)
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
                if (client is HorseClient hmqClient)
                    hmqClient.JsonSerializer = ContentSerializer;
            }

            base.ClientConnected(client);
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<HorseMessage> client, HorseMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_observer != null)
                _observer.Read((HorseClient) client, payload);
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
        public void On<T>(Action<T, HorseMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string queue, ushort content, Action<T, HorseMessage> action)
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
        public void OnDirect<T>(Action<T, HorseMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.OnDirect(action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T, HorseMessage> action)
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
        public bool Send(HorseMessage message)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Send(message);

            return false;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<HorseResult> SendAsync(HorseMessage message)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.SendAsync(message);

            return Task.FromResult(HorseResult.Failed());
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<HorseMessage> RequestAsync(HorseMessage message)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Request(message);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<HorseResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(TRequest request)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Direct.RequestJson<TResponse>(request);

            return null;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        public Task<HorseResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Direct.RequestJson<TResponse>(target, contentType, request);

            return null;
        }

        #endregion

        #region Push

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<HorseResult> Push(string queue, MemoryStream content, bool waitAcknowledge)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.Push(queue, content, waitAcknowledge);

            return Task.FromResult(HorseResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<HorseResult> PushJson(object jsonObject, bool waitAcknowledge)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(jsonObject, waitAcknowledge);

            return Task.FromResult(HorseResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<HorseResult> PushJson(string queue, object jsonObject, bool waitAcknowledge)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(queue, jsonObject, waitAcknowledge);

            return Task.FromResult(HorseResult.Failed());
        }

        #endregion

        #region Publish

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<HorseResult> Publish(string routerName, MemoryStream content, bool waitAcknowledge)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.Publish(routerName, content.ToArray(), null, waitAcknowledge);

            return Task.FromResult(HorseResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<HorseResult> PublishJson(object jsonObject, bool waitAcknowledge)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(jsonObject, waitAcknowledge);

            return Task.FromResult(HorseResult.Failed());
        }

        /// <summary>
        /// Publishes a message to the router
        /// </summary>
        public Task<HorseResult> PublishJson(string routerName, object jsonObject, bool waitAcknowledge)
        {
            HorseClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Routers.PublishJson(routerName, jsonObject, null, waitAcknowledge);

            return Task.FromResult(HorseResult.Failed());
        }

        #endregion
    }
}