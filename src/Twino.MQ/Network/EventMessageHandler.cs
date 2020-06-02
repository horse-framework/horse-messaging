using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Events;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class EventMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public EventMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        private static async Task SendResponse(MqClient client, TmqMessage message, bool successful)
        {
            ushort contentType = successful ? (ushort) TwinoResultCode.Ok : (ushort) TwinoResultCode.Failed;
            TmqMessage response = new TmqMessage(MessageType.Response, client.UniqueId, contentType);
            response.SetMessageId(message.MessageId);
            await client.SendAsync(response);
        }

        public Task Handle(MqClient client, TmqMessage message, bool fromNode)
        {
            string eventName = message.Target;
            string channelName = message.FindHeader(TmqHeaders.CHANNEL_NAME);
            string queueId = message.FindHeader(TmqHeaders.QUEUE_ID);
            bool subscribe = message.ContentType == 1;

            Channel channel = null;
            if (!string.IsNullOrEmpty(channelName))
                channel = _server.FindChannel(channelName);

            if (subscribe)
            {
                if (_server.Authorization != null)
                {
                    ushort authQueueId = 0;
                    if (!string.IsNullOrEmpty(queueId))
                        authQueueId = Convert.ToUInt16(queueId);

                    if (!_server.Authorization.CanSubscribeEvent(client, eventName, channelName, authQueueId))
                        return SendResponse(client, message, false);
                }
            }

            switch (eventName)
            {
                #region Queue Events

                case EventNames.MessageProduced:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    ChannelQueue queue = null;
                    if (!string.IsNullOrEmpty(queueId))
                        queue = channel.FindQueue(Convert.ToUInt16(queueId));

                    if (queue == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        queue.OnMessageProduced.Subscribe(client);
                    else
                        queue.OnMessageProduced.Unsubscribe(client);

                    return SendResponse(client, message, true);

                #endregion

                #region Server Events

                case EventNames.ClientConnected:
                    if (subscribe)
                        _server.OnClientConnected.Subscribe(client);
                    else
                        _server.OnClientConnected.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.ClientDisconnected:
                    if (subscribe)
                        _server.OnClientDisconnected.Subscribe(client);
                    else
                        _server.OnClientDisconnected.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.ChannelCreated:
                    if (subscribe)
                        _server.OnChannelCreated.Subscribe(client);
                    else
                        _server.OnChannelCreated.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.ChannelRemoved:
                    if (subscribe)
                        _server.OnChannelRemoved.Subscribe(client);
                    else
                        _server.OnChannelRemoved.Unsubscribe(client);

                    return SendResponse(client, message, true);

                #endregion

                #region Channel Events

                case EventNames.ClientJoined:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        channel.OnClientJoined.Subscribe(client);
                    else
                        channel.OnClientJoined.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.ClientLeft:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        channel.OnClientLeft.Subscribe(client);
                    else
                        channel.OnClientLeft.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.QueueCreated:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        channel.OnQueueCreated.Subscribe(client);
                    else
                        channel.OnQueueCreated.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.QueueUpdated:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        channel.OnQueueUpdated.Subscribe(client);
                    else
                        channel.OnQueueUpdated.Unsubscribe(client);

                    return SendResponse(client, message, true);

                case EventNames.QueueRemoved:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    if (subscribe)
                        channel.OnQueueRemoved.Subscribe(client);
                    else
                        channel.OnQueueRemoved.Unsubscribe(client);

                    return SendResponse(client, message, true);

                #endregion
            }

            return Task.CompletedTask;
        }
    }
}