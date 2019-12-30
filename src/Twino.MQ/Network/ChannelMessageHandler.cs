using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class ChannelMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public ChannelMessageHandler(MqServer server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MqClient client, TmqMessage message)
        {
            //find channel and queue
            Channel channel = _server.FindChannel(message.Target);

            //if auto creation active, try to create channel
            if (channel == null && _server.Options.AutoChannelCreation)
                channel = _server.CreateChannel(message.Target);

            if (channel == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
                queue = await channel.CreateQueue(message.ContentType);

            if (queue == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.NotFound));
                return;
            }

            //consumer is trying to pull from the queue
            //in false cases, we won't send any response, cuz client is pending only queue messages, not response messages
            if (message.Length == 0 && message.ResponseRequired)
            {
                //only pull statused queues can handle this request
                if (queue.Status != QueueStatus.Pull)
                    return;

                //client cannot pull message from the channel not in
                ChannelClient channelClient = channel.FindClient(client);
                if (channelClient == null)
                    return;

                //check authorization
                if (_server.Authorization != null)
                {
                    bool grant = await _server.Authorization.CanPullFromQueue(channelClient, queue);
                    if (!grant)
                        return;
                }

                await queue.Pull(channelClient, message);
            }

            //message have a content, this is the real message from producer to the queue
            else
            {
                //check authority
                if (_server.Authorization != null)
                {
                    bool grant = await _server.Authorization.CanMessageToQueue(client, queue, message);
                    if (!grant)
                    {
                        if (!string.IsNullOrEmpty(message.MessageId))
                            await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Unauthorized));
                        return;
                    }
                }

                //prepare the message
                QueueMessage queueMessage = new QueueMessage(message);
                queueMessage.Source = client;

                //push the message
                bool sent = await queue.Push(queueMessage, client);
                if (!sent)
                    await client.SendAsync(MessageBuilder.ResponseStatus(message, KnownContentTypes.Failed));
            }
        }
    }
}