using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
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

        private async Task SendResponse(MqClient client, TmqMessage message, bool successful)
        {
            throw new NotImplementedException();
        }

        public Task Handle(MqClient client, TmqMessage message)
        {
            string eventName = message.Target;
            string channelName = message.FindHeader(TmqHeaders.CHANNEL_NAME);
            bool subscribe = message.ContentType == 1;

            Channel channel = null;

            if (!string.IsNullOrEmpty(channelName))
                channel = _server.FindChannel(channelName);

            switch (eventName)
            {
                case EventNames.MessageProduced:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    string queueId = message.FindHeader(TmqHeaders.QUEUE_ID);

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

                case EventNames.ClientConnected:
                    break;

                case EventNames.ClientDisconnected:
                    break;

                case EventNames.ChannelCreated:
                    break;

                case EventNames.ChannelUpdated:
                    break;

                case EventNames.ChannelRemoved:
                    break;

                case EventNames.ClientJoined:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    break;

                case EventNames.ClientLeft:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    break;

                case EventNames.QueueCreated:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    break;

                case EventNames.QueueUpdated:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    break;

                case EventNames.QueueRemoved:
                    if (channel == null)
                        return SendResponse(client, message, false);

                    break;

                case EventNames.NodeConnected:
                    break;

                case EventNames.NodeDisconnected:
                    break;
            }

            return Task.CompletedTask;
        }
    }
}