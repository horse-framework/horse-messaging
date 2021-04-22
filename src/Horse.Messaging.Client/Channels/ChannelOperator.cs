using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    public class ChannelOperator
    {
        private readonly HorseClient _client;
        private readonly TypeDescriptorContainer<ChannelTypeDescriptor> _descriptorContainer;

        internal List<ChannelSubscriberRegistration> Registrations { get; } = new List<ChannelSubscriberRegistration>();

        internal ChannelOperator(HorseClient client)
        {
            _client = client;
            _descriptorContainer = new TypeDescriptorContainer<ChannelTypeDescriptor>(new ChannelTypeResolver());
        }

        internal async Task OnChannelMessage(HorseMessage message)
        {
            ChannelSubscriberRegistration reg = Registrations.FirstOrDefault(x => x.Name == message.Target);
            if (reg == null)
                return;

            object model = reg.MessageType == typeof(string)
                ? message.GetStringContent()
                : _client.MessageSerializer.Deserialize(message, reg.MessageType);

            try
            {
                await reg.Executer.Execute(_client, message, model);
            }
            catch (Exception ex)
            {
                _client.OnException("ChannelConsumer", ex, message);
            }
        }

        /// <summary>
        /// Subscribes to a channel
        /// </summary>
        public async Task<HorseResult> Subscribe(string channel, bool verifyResponse, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelSubscribe;
            message.SetTarget(channel);
            message.WaitResponse = verifyResponse;

            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    message.AddHeader(header.Key, header.Value);

            if (verifyResponse)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Unsubscribes from a channel
        /// </summary>
        public async Task<HorseResult> Unsubscribe(string channel, bool verifyResponse)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ChannelUnsubscribe;
            message.SetTarget(channel);
            message.WaitResponse = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, verifyResponse);
        }


        #region Publish

        /// <summary>
        /// Publishes a message to a channel
        /// </summary>
        public Task<HorseResult> PublishJson(object jsonObject, bool waitAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishJson(null, jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Publishes a message to a channel
        /// </summary>
        public async Task<HorseResult> PublishJson(string channel, object jsonObject, bool waitAcknowledge = false,
                                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            ChannelTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(jsonObject.GetType());

            if (!string.IsNullOrEmpty(channel))
                descriptor.Name = channel;

            HorseMessage message = descriptor.CreateMessage();
            message.WaitResponse = waitAcknowledge;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            message.Serialize(jsonObject, _client.MessageSerializer);

            if (string.IsNullOrEmpty(message.MessageId) && waitAcknowledge)
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            return await _client.WaitResponse(message, waitAcknowledge);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<HorseResult> Publish(string channel, string content, bool waitAcknowledge = false,
                                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await Publish(channel, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitAcknowledge, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<HorseResult> Publish(string channel, MemoryStream content, bool waitAcknowledge = false,
                                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage(MessageType.Channel, channel, KnownContentTypes.ChannelPush);
            message.Content = content;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.WaitResponse(message, waitAcknowledge);
        }

        #endregion
    }
}