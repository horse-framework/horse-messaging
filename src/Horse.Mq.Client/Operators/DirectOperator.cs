using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Mq.Client.Annotations.Resolvers;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Operators
{
    /// <summary>
    /// Direct message manager object for hmq client
    /// </summary>
    public class DirectOperator
    {
        private readonly HorseClient _client;

        internal DirectOperator(HorseClient client)
        {
            _client = client;
        }

        #region Send

        /// <summary>
        /// Sends a memory stream message
        /// </summary>
        public async Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge,
                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage();
            message.SetTarget(target);
            message.ContentType = contentType;
            message.Content = content;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendAndGetAck(message);

            return await _client.SendAsync(message);
        }

        /// <summary>
        /// Sends a JSON message by receiver name
        /// </summary>
        public async Task<HorseResult> SendJsonByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
                                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendJsonById("@name:" + name, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a JSON message by receiver type
        /// </summary>
        public async Task<HorseResult> SendJsonByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
                                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendJsonById("@type:" + type, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a JSON message by full name
        /// </summary>
        public async Task<HorseResult> SendJsonById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
                                                       IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage();
            message.SetTarget(id);
            message.Type = MessageType.DirectMessage;
            message.ContentType = contentType;
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendAndGetAck(message);

            return await _client.SendAsync(message);
        }

        /// <summary>
        /// Sends a JSON message by full name
        /// </summary>
        public async Task<HorseResult> SendJson<T>(T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(model.GetType());
            HorseMessage message = descriptor.CreateMessage(MessageType.DirectMessage, null, null);
            if (string.IsNullOrEmpty(message.Target))
                return new HorseResult(HorseResultCode.SendError);

            message.WaitResponse = waitAcknowledge;
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendAndGetAck(message);

            return await _client.SendAsync(message);
        }

        /// <summary>
        /// Sends a memory stream message by receiver name
        /// </summary>
        public async Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge,
                                                  IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendById("@name:" + name, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a memory stream message by receiver type
        /// </summary>
        public async Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge,
                                                  IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await SendById("@type:" + type, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Sends a memory stream message by full name
        /// </summary>
        private async Task<HorseResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge,
                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage();
            message.SetTarget(id);
            message.ContentType = contentType;
            message.Content = content;
            message.Type = MessageType.DirectMessage;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (waitAcknowledge)
                return await _client.SendAndGetAck(message);

            return await _client.SendAsync(message);
        }

        #endregion

        #region Request

        /// <summary>
        /// Sends a request to target with a JSON model, waits response
        /// </summary>
        public Task<HorseResult<TResponse>> RequestJson<TResponse>(object model,
                                                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return RequestJson<TResponse>(null, null, model, messageHeaders);
        }

        /// <summary>
        /// Sends a request to target with a JSON model, waits response
        /// </summary>
        public async Task<HorseResult<TResponse>> RequestJson<TResponse>(string target, ushort? contentType, object model,
                                                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(model.GetType());
            HorseMessage message = descriptor.CreateMessage(MessageType.DirectMessage, target, contentType);
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            HorseMessage responseMessage = await _client.Request(message);
            if (responseMessage.ContentType == 0)
            {
                TResponse response = responseMessage.Deserialize<TResponse>(_client.JsonSerializer);
                return new HorseResult<TResponse>(response, message, HorseResultCode.Ok);
            }

            return new HorseResult<TResponse>(default, responseMessage, (HorseResultCode) responseMessage.ContentType);
        }

        /// <summary>
        /// Sends a request to target, waits response
        /// </summary>
        public async Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content,
                                                IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);
            message.Content = content;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.Request(message);
        }

        /// <summary>
        /// Sends a request to target, waits response
        /// </summary>
        public async Task<HorseMessage> Request(string target, ushort contentType, string content,
                                                IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);
            message.SetStringContent(content);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.Request(message);
        }

        /// <summary>
        /// Sends a request to without body
        /// </summary>
        public async Task<HorseMessage> Request(string target, ushort contentType,
                                                IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage(MessageType.DirectMessage, target, contentType);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.Request(message);
        }

        #endregion
    }
}