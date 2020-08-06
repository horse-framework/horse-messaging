using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Client.TMQ.Bus;
using Twino.Client.TMQ.Exceptions;
using Twino.Client.TMQ.Internal;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Sticky connector for TMQ protocol.
    /// </summary>
    public class TmqStickyConnector : StickyConnector<TmqClient, TmqMessage>
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
            TmqClient tmqClient = (TmqClient) client;
            tmqClient.Events = Events;

            if (ContentSerializer != null)
                tmqClient.JsonSerializer = ContentSerializer;

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
    }
}