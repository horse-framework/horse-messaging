using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    /// <summary>
    /// Direct message manager object for Horse client
    /// </summary>
    public class DirectOperator
    {
        private readonly HorseClient _client;
        private readonly TypeDescriptorContainer<DirectTypeDescriptor> _descriptorContainer;

        internal List<DirectHandlerRegistration> Registrations { get; } = new List<DirectHandlerRegistration>();
        
        internal DirectOperator(HorseClient client)
        {
            _client = client;
            _descriptorContainer = new TypeDescriptorContainer<DirectTypeDescriptor>(new DirectTypeResolver());
        }

        internal async Task OnDirectMessage(HorseMessage message)
        {
            DirectHandlerRegistration reg = Registrations.FirstOrDefault(x => x.ContentType == message.ContentType);
            if (reg == null)
                return;

            object model = reg.MessageType == typeof(string)
                               ? message.GetStringContent()
                               : _client.MessageSerializer.Deserialize(message, reg.MessageType);

            try
            {
                await reg.ConsumerExecuter.Execute(_client, message, model);
            }
            catch (Exception ex)
            {
                _client.OnException(ex, message);
            }
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
            message.Serialize(model, _client.MessageSerializer);

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
            DirectTypeDescriptor descriptor = _descriptorContainer.GetDescriptor<T>();
            HorseMessage message = descriptor.CreateMessage();
            if (string.IsNullOrEmpty(message.Target))
                return new HorseResult(HorseResultCode.SendError);

            message.WaitResponse = waitAcknowledge;
            message.Serialize(model, _client.MessageSerializer);

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
            DirectTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(model.GetType());

            if (!string.IsNullOrEmpty(target))
                descriptor.DirectTarget = target;

            if (contentType.HasValue)
                descriptor.ContentType = contentType;
            
            HorseMessage message = descriptor.CreateMessage();
            
            message.Serialize(model, _client.MessageSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            HorseMessage responseMessage = await _client.Request(message);
            if (responseMessage.ContentType == 0)
            {
                TResponse response = responseMessage.Deserialize<TResponse>(_client.MessageSerializer);
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