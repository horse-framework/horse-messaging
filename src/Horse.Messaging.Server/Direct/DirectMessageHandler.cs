using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Direct
{
    internal class DirectMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseRider _rider;

        public DirectMessageHandler(HorseRider rider)
        {
            _rider = rider;
        }

        #endregion

        public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            if (string.IsNullOrEmpty(message.Target))
                return;

            if (message.Target.StartsWith("@name:"))
            {
                List<MessagingClient> receivers = _rider.Client.FindClientByName(message.Target.Substring(6));
                if (receivers.Count > 0)
                {
                    if (message.HighPriority && receivers.Count > 1)
                    {
                        MessagingClient first = receivers.FirstOrDefault();
                        receivers.Clear();
                        receivers.Add(first);
                    }

                    await ProcessMultipleReceiverClientMessage(client, receivers, message);
                }
                else if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                if (receivers.Count == 0)
                    foreach (IDirectMessageHandler handler in _rider.Direct.MessageHandlers.All())
                        _ = handler.OnNotFound(client, message);
            }
            else if (message.Target.StartsWith("@type:"))
            {
                List<MessagingClient> receivers = _rider.Client.FindByType(message.Target.Substring(6));
                if (receivers.Count > 0)
                {
                    if (message.HighPriority && receivers.Count > 1)
                    {
                        MessagingClient first = receivers.FirstOrDefault();
                        receivers.Clear();
                        receivers.Add(first);
                    }

                    await ProcessMultipleReceiverClientMessage(client, receivers, message);
                }
                else if (message.WaitResponse)
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                if (receivers.Count == 0)
                    foreach (IDirectMessageHandler handler in _rider.Direct.MessageHandlers.All())
                        _ = handler.OnNotFound(client, message);
            }
            else
                await ProcessSingleReceiverClientMessage(client, message);
        }


        /// <summary>
        /// Processes the client message which has multiple receivers (message by name or type)
        /// </summary>
        private async Task ProcessMultipleReceiverClientMessage(MessagingClient sender, List<MessagingClient> receivers, HorseMessage message)
        {
            if (receivers.Count < 1)
            {
                await sender.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
                return;
            }

            bool allDenied = true;
            foreach (MessagingClient receiver in receivers)
            {
                bool denied = false;

                //check sending message authority
                foreach (IClientAuthorization authorization in _rider.Client.Authorizations.All())
                {
                    bool grant = await authorization.CanDirectMessage(sender, message, receiver);
                    if (!grant)
                    {
                        await sender.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                        denied = true;
                        break;
                    }
                }

                if (denied)
                    continue;

                allDenied = false;
                receiver.Stats.ReceivedDirectMessages++;
                await receiver.SendAsync(message);
            }

            if (allDenied)
            {
                await sender.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                return;
            }

            sender.Stats.SentDirectMessages++;
            foreach (IDirectMessageHandler handler in _rider.Direct.MessageHandlers.All())
            {
                if (message.Type == MessageType.Response)
                    _ = handler.OnResponse(sender, message, receivers.FirstOrDefault());
                else
                    _ = handler.OnDirect(sender, message, receivers);
            }

            _rider.Direct.DirectEvent.Trigger(sender, message.Target, new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
        }

        /// <summary>
        /// Processes the client message which has single receiver (message by unique id)
        /// </summary>
        private async Task ProcessSingleReceiverClientMessage(MessagingClient client, HorseMessage message)
        {
            //find the receiver
            MessagingClient other = _rider.Client.Find(message.Target);
            if (other == null)
            {
                await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                foreach (IDirectMessageHandler handler in _rider.Direct.MessageHandlers.All())
                    _ = handler.OnNotFound(client, message);

                return;
            }

            //check sending message authority
            foreach (IClientAuthorization authorization in _rider.Client.Authorizations.All())
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

            foreach (IDirectMessageHandler handler in _rider.Direct.MessageHandlers.All())
            {
                if (message.Type == MessageType.Response)
                    _ = handler.OnResponse(client, message, other);
                else
                    _ = handler.OnDirect(client, message, new List<MessagingClient> {other});
            }

            _rider.Direct.DirectEvent.Trigger(client, message.Target,
                new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
        }
    }
}