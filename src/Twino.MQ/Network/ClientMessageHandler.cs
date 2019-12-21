using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class ClientMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public ClientMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            if (string.IsNullOrEmpty(message.Target))
                return;

            if (message.Target.StartsWith("@name:"))
            {
                List<MqClient> receivers = _server.FindClientByName(message.Target.Substring(6));
                await ProcessMultipleReceiverClientMessage(client, receivers, message);
            }
            else if (message.Target.StartsWith("@type:"))
            {
                List<MqClient> receivers = _server.FindClientByType(message.Target.Substring(6));
                await ProcessMultipleReceiverClientMessage(client, receivers, message);
            }
            else
                await ProcessSingleReceiverClientMessage(client, message);
        }


        /// <summary>
        /// Processes the client message which has multiple receivers (message by name or type)
        /// </summary>
        private async Task ProcessMultipleReceiverClientMessage(MqClient sender, List<MqClient> receivers, TmqMessage message)
        {
            if (receivers.Count < 1)
            {
                await sender.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            foreach (MqClient receiver in receivers)
            {
                //check sending message authority
                if (_server.Authorization != null)
                {
                    bool grant = await _server.Authorization.CanMessageToPeer(sender, message, receiver);
                    if (!grant)
                    {
                        await sender.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                        return;
                    }
                }

                //send the message
                await receiver.SendAsync(message);
            }
        }

        /// <summary>
        /// Processes the client message which has single receiver (message by unique id)
        /// </summary>
        private async Task ProcessSingleReceiverClientMessage(MqClient client, TmqMessage message)
        {
            //find the receiver
            MqClient other = _server.FindClient(message.Target);
            if (other == null)
            {
                await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //check sending message authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToPeer(client, message, other);
                if (!grant)
                {
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                    return;
                }
            }

            //send the message
            await other.SendAsync(message);
        }
    }
}