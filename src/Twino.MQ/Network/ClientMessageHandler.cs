using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
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
                if (receivers.Count > 0)
                {
                    if (message.FirstAcquirer && receivers.Count > 1)
                    {
                        MqClient first = receivers.FirstOrDefault();
                        receivers.Clear();
                        receivers.Add(first);
                    }

                    await ProcessMultipleReceiverClientMessage(client, receivers, message);
                }
                else if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResult.NotFound));
            }
            else if (message.Target.StartsWith("@type:"))
            {
                List<MqClient> receivers = _server.FindClientByType(message.Target.Substring(6));
                if (receivers.Count > 0)
                {
                    if (message.FirstAcquirer)
                    {
                        MqClient first = receivers.FirstOrDefault();
                        receivers.Clear();
                        receivers.Add(first);
                    }

                    await ProcessMultipleReceiverClientMessage(client, receivers, message);
                }
                else if (message.PendingResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResult.NotFound));
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
                await sender.SendAsync(message.CreateResponse(TwinoResult.NotFound));
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
                        await sender.SendAsync(message.CreateResponse(TwinoResult.Unauthorized));
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
                await client.SendAsync(message.CreateResponse(TwinoResult.NotFound));
                return;
            }

            //check sending message authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToPeer(client, message, other);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(TwinoResult.Unauthorized));
                    return;
                }
            }

            //send the message
            await other.SendAsync(message);
        }
    }
}