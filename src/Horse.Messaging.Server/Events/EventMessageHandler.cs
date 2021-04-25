using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Events
{
    internal class EventMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseRider _rider;

        public EventMessageHandler(HorseRider rider)
        {
            _rider = rider;
        }

        #endregion

        private static async Task SendResponse(MessagingClient client, HorseMessage message, bool successful)
        {
            ushort contentType = successful ? (ushort) HorseResultCode.Ok : (ushort) HorseResultCode.Failed;
            HorseMessage response = new HorseMessage(MessageType.Response, client.UniqueId, contentType);
            response.SetMessageId(message.MessageId);
            await client.SendAsync(response);
        }

        public Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            HorseEventType type = (HorseEventType) message.ContentType;
            bool subscribe = true;
            string s = message.FindHeader(HorseHeaders.SUBSCRIBE);
            if (!string.IsNullOrEmpty(s) == s.Equals("NO", StringComparison.CurrentCultureIgnoreCase))
                subscribe = false;

            if (subscribe)
            {
                foreach (IClientAuthorization authorization in _rider.Client.Authorizations.All())
                {
                    if (!authorization.CanSubscribeEvent(client, type, message.Target))
                        return SendResponse(client, message, false);
                }
            }

            switch (type)
            {
                case HorseEventType.CacheGet:
                    break;
            }

            return Task.CompletedTask;
        }
    }
}