using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations.Resolvers;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Operators
{
    /// <summary>
    /// Direct message manager object for tmq client
    /// </summary>
    public class DirectOperator
    {
        private readonly TmqClient _client;

        internal DirectOperator(TmqClient client)
        {
            _client = client;
        }

        #region Send

        /// <summary>
        /// Sends a memory stream message
        /// </summary>
        public async Task<TwinoResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage message = new TwinoMessage();
            message.SetTarget(target);
            message.ContentType = contentType;
            message.Content = content;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendWithAcknowledge(message);

            return await _client.SendAsync(message);
        }

        /// <summary>
        /// Sends a JSON message by receiver name
        /// </summary>
        public async Task<TwinoResult> SendJsonByName<T>(string name, ushort contentType, T model, bool toOnlyFirstReceiver, bool waitAcknowledge,
                                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendJsonById("@name:" + name, contentType, model, toOnlyFirstReceiver, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a JSON message by receiver type
        /// </summary>
        public async Task<TwinoResult> SendJsonByType<T>(string type, ushort contentType, T model, bool toOnlyFirstReceiver, bool waitAcknowledge,
                                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendJsonById("@type:" + type, contentType, model, toOnlyFirstReceiver, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a JSON message by full name
        /// </summary>
        public async Task<TwinoResult> SendJsonById<T>(string id, ushort contentType, T model, bool toOnlyFirstReceiver, bool waitAcknowledge,
                                                       IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage message = new TwinoMessage();
            message.SetTarget(id);
            message.Type = MessageType.DirectMessage;
            message.FirstAcquirer = toOnlyFirstReceiver;
            message.ContentType = contentType;
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendWithAcknowledge(message);

            return await _client.SendAsync(message);
        }

        /// <summary>
        /// Sends a JSON message by full name
        /// </summary>
        public async Task<TwinoResult> SendJson<T>(T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(model.GetType());
            TwinoMessage message = descriptor.CreateMessage(MessageType.DirectMessage, null, null);
            if (string.IsNullOrEmpty(message.Target))
                return new TwinoResult(TwinoResultCode.SendError);

            message.PendingAcknowledge = waitAcknowledge;
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendWithAcknowledge(message);

            return await _client.SendAsync(message);
        }

        /// <summary>
        /// Sends a memory stream message by receiver name
        /// </summary>
        public async Task<TwinoResult> SendByName(string name, ushort contentType, MemoryStream content, bool toOnlyFirstReceiver, bool waitAcknowledge,
                                                  IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendById("@name:" + name, contentType, content, toOnlyFirstReceiver, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a memory stream message by receiver type
        /// </summary>
        public async Task<TwinoResult> SendByType(string type, ushort contentType, MemoryStream content, bool toOnlyFirstReceiver, bool waitAcknowledge,
                                                  IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendById("@type:" + type, contentType, content, toOnlyFirstReceiver, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a memory stream message by full name
        /// </summary>
        private async Task<TwinoResult> SendById(string id, ushort contentType, MemoryStream content, bool toOnlyFirstReceiver, bool waitAcknowledge,
                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage message = new TwinoMessage();
            message.SetTarget(id);
            message.FirstAcquirer = toOnlyFirstReceiver;
            message.ContentType = contentType;
            message.Content = content;
            message.Type = MessageType.DirectMessage;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendWithAcknowledge(message);

            return await _client.SendAsync(message);
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a request to target with a JSON model, waits response
        /// </summary>
        public Task<TwinoResult<TResponse>> RequestJson<TResponse>(object model,
                                                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return RequestJson<TResponse>(null, null, model, messageHeaders);
        }

        /// <summary>
        /// Sends a request to target with a JSON model, waits response
        /// </summary>
        public async Task<TwinoResult<TResponse>> RequestJson<TResponse>(string target, ushort? contentType, object model,
                                                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(model.GetType());
            TwinoMessage message = descriptor.CreateMessage(MessageType.DirectMessage, target, contentType);
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            TwinoMessage responseMessage = await _client.Request(message);
            if (responseMessage.ContentType == 0)
            {
                TResponse response = responseMessage.Deserialize<TResponse>(_client.JsonSerializer);
                return new TwinoResult<TResponse>(response, message, TwinoResultCode.Ok);
            }

            return new TwinoResult<TResponse>(default, responseMessage, (TwinoResultCode) responseMessage.ContentType);
        }

        /// <summary>
        /// Sends a request to target, waits response
        /// </summary>
        public async Task<TwinoMessage> Request(string target, ushort contentType, MemoryStream content,
                                              IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage message = new TwinoMessage(MessageType.DirectMessage, target, contentType);
            message.Content = content;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.Request(message);
        }

        /// <summary>
        /// Sends a request to target, waits response
        /// </summary>
        public async Task<TwinoMessage> Request(string target, ushort contentType, string content,
                                              IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage message = new TwinoMessage(MessageType.DirectMessage, target, contentType);
            message.SetStringContent(content);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.Request(message);
        }

        /// <summary>
        /// Sends a request to without body
        /// </summary>
        public async Task<TwinoMessage> Request(string target, ushort contentType,
                                              IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage message = new TwinoMessage(MessageType.DirectMessage, target, contentType);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.Request(message);
        }

        #endregion
    }
}