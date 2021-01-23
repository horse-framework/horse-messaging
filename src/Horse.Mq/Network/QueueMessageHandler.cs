using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Mq.Clients;
using Horse.Mq.Options;
using Horse.Mq.Queues;
using Horse.Mq.Security;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Network
{
    internal class QueueMessageHandler : INetworkMessageHandler
    {
        #region Fields

        /// <summary>
        /// Messaging Queue Server
        /// </summary>
        private readonly HorseMq _server;

        public QueueMessageHandler(HorseMq server)
        {
            _server = server;
        }

        #endregion

        private async Task<HorseQueue> FindQueue(MqClient client, string name, HorseMessage message)
        {
            HorseQueue queue = _server.FindQueue(name);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
            {
                QueueOptions options = QueueOptions.CloneFrom(_server.Options);
                queue = await _server.CreateQueue(name, options, message, _server.DeliveryHandlerFactory, true, true);
            }

            if (queue == null)
            {
                if (client != null && message != null && !string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

                return null;
            }

            return queue;
        }

        public async Task Handle(MqClient client, HorseMessage message, bool fromNode)
        {
            HorseQueue queue = await FindQueue(client, message.Target, message);
            if (queue == null)
                return;

            //if there is at least one cc header
            //we need to create a clone of the message
            //clone does not have cc headers but others
            HorseMessage clone = null;
            List<string> ccList = null;
            List<KeyValuePair<string, string>> additionalHeaders = null;
            if (message.HasHeader && message.FindHeader(HorseHeaders.CC) != null)
            {
                additionalHeaders = message.Headers.Where(x => !x.Key.Equals(HorseHeaders.CC, StringComparison.InvariantCultureIgnoreCase)).ToList();
                ccList = new List<string>(message.Headers.Where(x => x.Key.Equals(HorseHeaders.CC, StringComparison.InvariantCultureIgnoreCase)).Select(x => x.Value));
                clone = message.Clone(false, true, _server.MessageIdGenerator.Create(), additionalHeaders);
            }

            await HandlePush(client, message, queue, true);

            //if there are cc headers, we will push the message to other queues
            if (clone != null)
                await PushOtherQueues(client, clone, ccList, additionalHeaders);
        }

        /// <summary>
        /// Handles pushing a message into a queue
        /// </summary>
        private async Task HandlePush(MqClient client, HorseMessage message, HorseQueue queue, bool answerSender)
        {
            //check authority
            foreach (IClientAuthorization authorization in _server.Authorizations)
            {
                bool grant = await authorization.CanMessageToQueue(client, queue, message);
                if (!grant)
                {
                    if (answerSender && !string.IsNullOrEmpty(message.MessageId))
                        await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
                    return;
                }
            }

            //prepare the message
            QueueMessage queueMessage = new QueueMessage(message);
            queueMessage.Source = client;

            //push the message
            PushResult result = await queue.Push(queueMessage, client);
            if (result == PushResult.StatusNotSupported)
            {
                if (answerSender)
                    await client.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
            }
            else if (result == PushResult.LimitExceeded)
            {
                if (answerSender)
                    await client.SendAsync(message.CreateResponse(HorseResultCode.LimitExceeded));
            }
        }

        /// <summary>
        /// Pushes clones of the message to cc queues
        /// </summary>
        private async Task PushOtherQueues(MqClient client, HorseMessage clone, List<string> ccList, List<KeyValuePair<string, string>> additionalHeaders)
        {
            for (int i = 0; i < ccList.Count; i++)
            {
                string cc = ccList[i];

                string[] split = cc.Split(';');
                if (split.Length < 1)
                    continue;

                string queueName = split[0].Trim();
                string messageId = null;
                if (split.Length > 1)
                    messageId = split[1];

                HorseQueue queue = await FindQueue(null, queueName, clone);
                if (queue == null)
                    continue;

                HorseMessage msg = clone;
                if (i < ccList.Count - 1)
                    clone = clone.Clone(false, true, _server.MessageIdGenerator.Create(), additionalHeaders);

                if (!string.IsNullOrEmpty(messageId))
                    msg.SetMessageId(messageId);

                _ = HandlePush(client, msg, queue, false);
            }
        }
    }
}