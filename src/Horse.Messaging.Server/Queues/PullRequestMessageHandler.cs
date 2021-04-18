using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Queues
{
    internal class PullRequestMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        public PullRequestMessageHandler(HorseMq server)
        {
            _server = server;
        }

        #endregion

        public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
        {
            try
            {
                HorseQueue queue = _server.FindQueue(message.Target);

                //if auto creation active, try to create queue
                if (queue == null && _server.Options.AutoQueueCreation)
                {
                    QueueOptions options = QueueOptions.CloneFrom(_server.Options);
                    queue = await _server.CreateQueue(message.Target, options, message, _server.DeliveryHandlerFactory, true, true);
                }

                if (queue == null)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                    return;
                }

                await HandlePullRequest(client, message, queue);
            }
            catch (Exception e)
            {
                _server.SendError("PULL_REQUEST", e, $"QueueName:{message.Target}");
            }
        }


        /// <summary>
        /// Handles pulling a message from a queue
        /// </summary>
        private async Task HandlePullRequest(MessagingClient client, HorseMessage message, HorseQueue queue)
        {
            //only pull statused queues can handle this request
            if (queue.Type != QueueType.Pull)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, HorseHeaders.UNACCEPTABLE));

                return;
            }

            //client cannot pull message from the queue not in
            QueueClient queueClient = queue.FindClient(client);
            if (queueClient == null)
            {
                if (!string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, HorseHeaders.UNACCEPTABLE));

                return;
            }

            //check authorization
            foreach (IClientAuthorization authorization in _server.Authorizations)
            {
                bool grant = await authorization.CanPullFromQueue(queueClient, queue);
                if (!grant)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(MessageBuilder.CreateNoContentPullResponse(message, HorseHeaders.UNAUTHORIZED));
                    return;
                }
            }

            await queue.State.Pull(queueClient, message);
        }
    }
}