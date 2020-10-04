using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class DirectMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly TwinoMQ _server;

        public DirectMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TwinoMessage message, bool fromNode)
        {
            if (string.IsNullOrEmpty(message.Target))
                return;

            if (message.Target.StartsWith("@name:"))
            {
                List<MqClient> receivers = _server.FindClientByName(message.Target.Substring(6));
                if (receivers.Count > 0)
                {
                    if (message.HighPriority && receivers.Count > 1)
                    {
                        MqClient first = receivers.FirstOrDefault();
                        receivers.Clear();
                        receivers.Add(first);
                    }

                    await ProcessMultipleReceiverClientMessage(client, receivers, message);
                }
                else if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
            }
            else if (message.Target.StartsWith("@type:"))
            {
                List<MqClient> receivers = _server.FindClientByType(message.Target.Substring(6));
                if (receivers.Count > 0)
                {
                    if (message.HighPriority)
                    {
                        MqClient first = receivers.FirstOrDefault();
                        receivers.Clear();
                        receivers.Add(first);
                    }

                    await ProcessMultipleReceiverClientMessage(client, receivers, message);
                }
                else if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
            }
            else
                await ProcessSingleReceiverClientMessage(client, message);
        }


        /// <summary>
        /// Processes the client message which has multiple receivers (message by name or type)
        /// </summary>
        private async Task ProcessMultipleReceiverClientMessage(MqClient sender, List<MqClient> receivers, TwinoMessage message)
        {
            if (receivers.Count < 1)
            {
                await sender.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            foreach (MqClient receiver in receivers)
            {
                //check sending message authority
                foreach (IClientAuthorization authorization in _server.Authorizations)
                {
                    bool grant = await authorization.CanDirectMessage(sender, message, receiver);
                    if (!grant)
                    {
                        await sender.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
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
        private async Task ProcessSingleReceiverClientMessage(MqClient client, TwinoMessage message)
        {
            //find the receiver
            MqClient other = _server.FindClient(message.Target);
            if (other == null)
            {
                await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            //check sending message authority
            foreach (IClientAuthorization authorization in _server.Authorizations)
            {
                bool grant = await authorization.CanDirectMessage(client, message, other);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                    return;
                }
            }

            //send the message
            await other.SendAsync(message);
        }
    }
}