using System;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Sticky connector for TMQ protocol.
    /// </summary>
    public class TmqStickyConnector : StickyConnector<TmqClient, TmqMessage>
    {
        private MessageReader _reader;
        public MessageReader Reader => _reader;

        public TmqStickyConnector(TimeSpan reconnectInterval, Func<TmqClient> createInstance = null)
            : base(reconnectInterval, createInstance)
        {
        }

        /// <summary>
        /// Inits the JSON reader on the connector
        /// </summary>
        public void InitJsonReader()
        {
            _reader = MessageReader.JsonReader();
        }

        /// <summary>
        /// Inits a custom reader on the connector
        /// </summary>
        public void InitReader(Func<TmqMessage, Type, object> serailizationAction)
        {
            _reader = new MessageReader(serailizationAction);
        }

        protected override void ClientMessageReceived(ClientSocketBase<TmqMessage> client, TmqMessage payload)
        {
            base.ClientMessageReceived(client, payload);

            if (_reader != null)
                _reader.Read((TmqClient) client, payload);
        }

        /// <summary>
        /// Subscribes from reading messages in a queue
        /// </summary>
        public void On<T>(string channel, ushort content, Action<T> action)
        {
            if (_reader == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _reader.On(channel, content, action);
        }

        /// <summary>
        /// Unsubscribes from reading messages in a queue
        /// </summary>
        public void Off(string channel, ushort content)
        {
            if (_reader == null)
                throw new NullReferenceException("Reader is null. Please init reader first with InitReader methods");

            _reader.Off(channel, content);
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
        public async Task<bool> SendAsync(TmqMessage message)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return await client.SendAsync(message);

            return false;
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public async Task<bool> Push(string channel, ushort contentType, MemoryStream content, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return await client.Push(channel, contentType, content, waitAcknowledge);

            return false;
        }

        /// <summary>
        /// Pushes a message to the queue
        /// </summary>
        public async Task<bool> PushJson(string channel, ushort contentType, object jsonObject, bool waitAcknowledge)
        {
            TmqClient client = GetClient();
            if (client != null && client.IsConnected)
                return await client.PushJson(channel, contentType, jsonObject, waitAcknowledge);

            return false;
        }
    }
}