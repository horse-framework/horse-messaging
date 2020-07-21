using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Single message connector for TMQ protocol.
    /// </summary>
    public class TmqSingleMessageConnector : SingleMessageConnector<TmqClient, TmqMessage>
    {
        private MessageConsumer _consumer;

        /// <summary>
        /// Default TMQ Message reader for connector
        /// </summary>
        public MessageConsumer Consumer => _consumer;

        /// <summary>
        /// Creates new single message connector for TMQ protocol clients
        /// </summary>
        public TmqSingleMessageConnector(Func<TmqClient> createInstance = null) : base(createInstance)
        {
        }

        /// <summary>
        /// Inits the JSON reader on the connector
        /// </summary>
        public void InitJsonReader()
        {
            _consumer = MessageConsumer.JsonReader();
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

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _consumer.On(channel, content, action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _consumer.OnDirect(content, action);
        }

        /// <summary>
        /// Subscribes for reading direct messages
        /// </summary>
        public void OnDirect<T>(ushort content, Action<T, TmqMessage> action)
        {
            if (_consumer == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _consumer.OnDirect(content, action);
        }

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off(string channel, ushort content)
        {
            if (_consumer == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _consumer.Off(channel, content);
        }

        /// <summary>
        /// Unsubscribes from reading direct messages
        /// </summary>
        public void OffDirect(ushort content)
        {
            if (_consumer == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _consumer.Off(content);
        }

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
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> Push(string channel, ushort contentType, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.Push(channel, contentType, content, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public Task<TwinoResult> PushJson(string channel, ushort contentType, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return client.Queues.PushJson(channel, contentType, jsonObject, waitAcknowledge);

            return Task.FromResult(TwinoResult.Failed());
        }
    }
}