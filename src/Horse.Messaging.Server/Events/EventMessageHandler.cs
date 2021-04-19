using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Queues;
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
            string eventName = message.Target;
            string queueName = message.FindHeader(HorseHeaders.QUEUE_NAME);
            bool subscribe = message.ContentType == 1;

            HorseQueue queue = !string.IsNullOrEmpty(queueName) ? _rider.Queue.FindQueue(queueName) : null;
            if (subscribe)
            {
                foreach (IClientAuthorization authorization in _rider.Client.Authorizations.All())
                {
                    if (!authorization.CanSubscribeEvent(client, queue))
                        return SendResponse(client, message, false);
                }
            }

            switch (eventName)
            {
                case EventNames.MessageProduced:
                    if (queue == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        queue.OnMessageProduced.Subscribe(client);
                    else
                        queue.OnMessageProduced.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.ClientConnected:
                    if (subscribe)
                        _rider.Client.OnClientConnected.Subscribe(client);
                    else
                        _rider.Client.OnClientConnected.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.ClientDisconnected:
                    if (subscribe)
                        _rider.Client.OnClientDisconnected.Subscribe(client);
                    else
                        _rider.Client.OnClientDisconnected.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.Subscribe:
                    if (queue == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        queue.OnConsumerSubscribed.Subscribe(client);
                    else
                        queue.OnConsumerSubscribed.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.Unsubscribe:
                    if (queue == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        queue.OnConsumerUnsubscribed.Subscribe(client);
                    else
                        queue.OnConsumerUnsubscribed.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.QueueCreated:
                    if (subscribe)
                        _rider.Queue.OnQueueCreated.Subscribe(client);
                    else
                        _rider.Queue.OnQueueCreated.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.QueueUpdated:
                    if (subscribe)
                        _rider.Queue.OnQueueUpdated.Subscribe(client);
                    else
                        _rider.Queue.OnQueueUpdated.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.QueueRemoved:
                    if (subscribe)
                        _rider.Queue.OnQueueRemoved.Subscribe(client);
                    else
                        _rider.Queue.OnQueueRemoved.Unsubscribe(client);

                    return SendResponse(client, message, true);
            }

            return Task.CompletedTask;
        }
    }
}