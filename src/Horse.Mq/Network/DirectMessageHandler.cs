using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Security;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Network
{
    internal class DirectMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        public DirectMessageHandler(HorseMq server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, HorseMessage message, bool fromNode)
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
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                if (receivers.Count == 0)
                    foreach (IDirectMessageHandler handler in _server.DirectMessageHandlers)
                        _ = handler.OnNotFound(client, message);
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
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                if (receivers.Count == 0)
                    foreach (IDirectMessageHandler handler in _server.DirectMessageHandlers)
                        _ = handler.OnNotFound(client, message);
            }
            else
                await ProcessSingleReceiverClientMessage(client, message);
        }


        /// <summary>
        /// Processes the client message which has multiple receivers (message by name or type)
        /// </summary>
        private async Task ProcessMultipleReceiverClientMessage(MqClient sender, List<MqClient> receivers, HorseMessage message)
        {
            if (receivers.Count < 1)
            {
                await sender.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
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
                        await sender.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        return;
                    }
                }

                //send the message
                await receiver.SendAsync(message);
            }

            foreach (IDirectMessageHandler handler in _server.DirectMessageHandlers)
                _ = handler.OnDirect(sender, message, receivers);
        }

        /// <summary>
        /// Processes the client message which has single receiver (message by unique id)
        /// </summary>
        private async Task ProcessSingleReceiverClientMessage(MqClient client, HorseMessage message)
        {
            //find the receiver
            MqClient other = _server.FindClient(message.Target);
            if (other == null)
            {
                await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                foreach (IDirectMessageHandler handler in _server.DirectMessageHandlers)
                    _ = handler.OnNotFound(client, message);

                return;
            }

            //check sending message authority
            foreach (IClientAuthorization authorization in _server.Authorizations)
            {
                bool grant = await authorization.CanDirectMessage(client, message, other);
                if (!grant)
                {
                    await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    return;
                }
            }

            //send the message
            await other.SendAsync(message);

            foreach (IDirectMessageHandler handler in _server.DirectMessageHandlers)
                _ = handler.OnDirect(client, message, new List<MqClient> {other});
        }
    }
}