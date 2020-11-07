using System;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Core;
using Twino.MQ.Client.Annotations.Resolvers;
using Twino.MQ.Client.Bus;
using Twino.MQ.Client.Exceptions;
using Twino.MQ.Client.Internal;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Connectors
{
    /// <summary>
    /// Sticky connector for TMQ protocol.
    /// </summary>
    public class TmqStickyConnector : StickyConnector<TmqClient, TwinoMessage>
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
        /// If true, disconnected from server when auto join fails
        /// </summary>
        public bool DisconnectionOnAutoJoinFailure { get; set; } = true;

        /// <summary>
        /// Content Serializer for clients in this connector.
        /// If null, default content serializer will be used.
        /// </summary>
        public IMessageContentSerializer ContentSerializer { get; set; }

        /// <summary>
        /// Event manager for clients
        /// </summary>
        internal EventManager Events { get; set; }

        /// <summary>
        /// Message bus for the connector
        /// </summary>
        public ITwinoBus Bus { get; protected set; }

        /// <summary>
        /// Creates new sticky connector for TMQ protocol clients
        /// </summary>
        public TmqStickyConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
            _observer = new MessageObserver(ReadMessage);
            Events = new EventManager();
            Bus = new TwinoBus(this);
        }

        private object ReadMessage(TwinoMessage message, Type type)
        {
            if (ContentSerializer == null)
                ContentSerializer = new NewtonsoftContentSerializer();

            return ContentSerializer.Deserialize(message, type);
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<TwinoMessage> client, TwinoMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_observer != null)
                _observer.Read((TmqClient) client, payload);
        }

        /// <inheritdoc />
        protected override void ClientConnected(SocketBase client)
        {
            TmqClient tmqClient = (TmqClient) client;
            
            tmqClient.Events = Events;
            if (tmqClient.DeliveryContainer is TypeDeliveryContainer container)
                container.DefaultConfiguration = Observer.Configurator;

            if (ContentSerializer != null)
                tmqClient.JsonSerializer = ContentSerializer;

            base.ClientConnected(client);

            if (AutoSubscribe)
                _ = SubscribeToAllImplementedQueues(true, DisconnectionOnAutoJoinFailure);
        }

        /// <summary>
        /// Subscribes to all implemenetd queues (Implemented with IQueueConsumer interface)
        /// </summary>
        /// <param name="verify">If true, waits response from server for each join operation</param>
        /// <param name="disconnectOnFail">If any of queues fails to subscribe, disconnected from server</param>
        /// <param name="silent">If true, errors are hidden, no exception thrown</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">Thrown if there is no consumer initialized</exception>
        /// <exception cref="TwinoSocketException">Thrown if not connected to server</exception>
        public async Task<bool> SubscribeToAllImplementedQueues(bool verify, bool disconnectOnFail = true, bool silent = true)
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

            string[] queues = _observer.GetSubscribedQueues();
            foreach (string queue in queues)
            {
                TwinoResult joinResult = await client.Queues.Subscribe(queue, verify);
                if (joinResult.Code == TwinoResultCode.Ok)
                    continue;

                if (disconnectOnFail)
                    client.Disconnect();

                if (!silent)
                    throw new TwinoQueueException($"Can't subscribe to {queue} queue: {joinResult.Reason} ({joinResult.Code})");

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
        public void On<T>(string queue, Action<T, TwinoMessage> action)
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
    }
}