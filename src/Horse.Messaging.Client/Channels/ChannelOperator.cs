using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    public class ChannelOperator
    {
        private readonly HorseClient _client;

        internal List<ChannelSubscriberRegistration> Registrations { get; } = new List<ChannelSubscriberRegistration>();

        internal ChannelOperator(HorseClient client)
        {
            _client = client;
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

        //send


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
    }
}