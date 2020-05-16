using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Network
{
    internal class QueueMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly MqServer _server;

        public QueueMessageHandler(MqServer server)
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
                channel = _server.FindOrCreateChannel(message.Target);

            if (channel == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                return;
            }

            ChannelQueue queue = channel.FindQueue(message.ContentType);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
                queue = await channel.FindOrCreateQueue(message.ContentType);

            if (queue == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));
                
                return;
            }

            await HandlePush(client, message, queue);
        }

        /// <summary>
        /// Handles pushing a message into a queue
        /// </summary>
        private async Task HandlePush(MqClient client, TmqMessage message, ChannelQueue queue)
        {
            //check authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToQueue(client, queue, message);
                if (!grant)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
                    return;
                }
            }

            //prepare the message
            QueueMessage queueMessage = new QueueMessage(message);
            queueMessage.Source = client;

            //push the message
            PushResult result = await queue.Push(queueMessage, client);
            if (result == PushResult.StatusNotSupported)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
            else if (result == PushResult.LimitExceeded)
                await client.SendAsync(message.CreateResponse(TwinoResultCode.LimitExceeded));
        }
    }
}