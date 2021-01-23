using System;
using System.Threading.Tasks;
using Horse.Client.Connectors;
using Horse.Core;
using Horse.Mq.Client.Annotations.Resolvers;
using Horse.Mq.Client.Bus;
using Horse.Mq.Client.Exceptions;
using Horse.Mq.Client.Internal;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Connectors
{
    /// <summary>
    /// Sticky connector for HMQ protocol.
    /// </summary>
    public class HmqStickyConnector : StickyConnector<HorseClient, HorseMessage>
    {
        private readonly MessageObserver _observer;

        /// <summary>
        /// Default HMQ Message reader for connector
        /// </summary>
        public MessageObserver Observer => _observer;

        /// <summary>
        /// If true, automatically subscribes all implemented IQueueConsumer queues
        /// </summary>
        public bool AutoSubscribe { get; set; } = true;

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
        public IHorseBus Bus { get; protected set; }

        /// <summary>
        /// Creates new sticky connector for HMQ protocol clients
        /// </summary>
        public HmqStickyConnector(TimeSpan reconnectInterval, Func<HorseClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
            _observer = new MessageObserver(ReadMessage);
            Events = new EventManager();
            Bus = new HorseBus(this);
        }

        private object ReadMessage(HorseMessage message, Type type)
        {
            if (ContentSerializer == null)
                ContentSerializer = new NewtonsoftContentSerializer();

            return ContentSerializer.Deserialize(message, type);
        }

        /// <inheritdoc />
        protected override void ClientMessageReceived(ClientSocketBase<HorseMessage> client, HorseMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_observer != null)
                _observer.Read((HorseClient) client, payload);
        }

        /// <inheritdoc />
        protected override void ClientConnected(SocketBase client)
        {
            HorseClient horseClient = (HorseClient) client;
            
            horseClient.Events = Events;
            if (horseClient.DeliveryContainer is TypeDeliveryContainer container)
                container.DefaultConfiguration = Observer.Configurator;

            if (ContentSerializer != null)
                horseClient.JsonSerializer = ContentSerializer;

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
        /// <exception cref="HorseSocketException">Thrown if not connected to server</exception>
        public async Task<bool> SubscribeToAllImplementedQueues(bool verify, bool disconnectOnFail = true, bool silent = true)
        {
            if (_observer == null)
            {
                if (silent)
                    return false;

                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");
            }

            HorseClient client = GetClient();
            if (client == null)
            {
                if (silent)
                    return false;

                throw new HorseSocketException("There is no active connection");
            }

            Tuple<string, TypeDeliveryDescriptor>[] items = _observer.GetSubscribedQueues();
            foreach (Tuple<string, TypeDeliveryDescriptor> item in items)
            {
                HorseMessage msg = item.Item2.CreateMessage(MessageType.Server, item.Item1, KnownContentTypes.Subscribe);
                HorseResult joinResult = await client.SendAndGetAck(msg);
                if (joinResult.Code == HorseResultCode.Ok)
                    continue;

                if (disconnectOnFail)
                    client.Disconnect();

                if (!silent)
                    throw new HorseQueueException($"Can't subscribe to {item.Item1} queue: {joinResult.Reason} ({joinResult.Code})");

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
        public void On<T>(Action<T, HorseMessage> action)
        {
            if (_observer == null)
                throw new NullReferenceException("Consumer is null. Please init consumer first with InitReader methods");

            _observer.On(action);
        }


        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string queue, Action<T, HorseMessage> action)
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
    }
}