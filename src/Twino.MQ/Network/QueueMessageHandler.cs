using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Options;
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
        private readonly TwinoMQ _server;

        public QueueMessageHandler(TwinoMQ server)
        {
            _server = server;
        }

        #endregion

        private async Task<TwinoQueue> FindQueue(MqClient client, string name, TwinoMessage message)
        {
            TwinoQueue queue = _server.FindQueue(name);

            //if auto creation active, try to create queue
            if (queue == null && _server.Options.AutoQueueCreation)
            {
                QueueOptions options = QueueOptions.CloneFrom(_server.Options);
                queue = await _server.CreateQueue(name, options, message, _server.DeliveryHandlerFactory);
            }

            if (queue == null)
            {
                if (client != null && message != null && !string.IsNullOrEmpty(message.MessageId))
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.NotFound));

                return null;
            }

            return queue;
        }

        public async Task Handle(MqClient client, TwinoMessage message, bool fromNode)
        {
            TwinoQueue queue = await FindQueue(client, message.Target, message);
            if (queue == null)
                return;

            //if there is at least one cc header
            //we need to create a clone of the message
            //clone does not have cc headers but others
            TwinoMessage clone = null;
            List<string> ccList = null;
            List<KeyValuePair<string, string>> additionalHeaders = null;
            if (message.HasHeader)
            {
                additionalHeaders = message.Headers.Where(x => !x.Key.Equals(TwinoHeaders.CC, StringComparison.InvariantCultureIgnoreCase)).ToList();
                ccList = new List<string>(message.Headers.Where(x => x.Key.Equals(TwinoHeaders.CC, StringComparison.InvariantCultureIgnoreCase)).Select(x => x.Value));
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
        private async Task HandlePush(MqClient client, TwinoMessage message, TwinoQueue queue, bool answerSender)
        {
            //check authority
            if (_server.Authorization != null)
            {
                bool grant = await _server.Authorization.CanMessageToQueue(client, queue, message);
                if (!grant)
                {
                    if (answerSender && !string.IsNullOrEmpty(message.MessageId))
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
            {
                if (answerSender)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.Unauthorized));
            }
            else if (result == PushResult.LimitExceeded)
            {
                if (answerSender)
                    await client.SendAsync(message.CreateResponse(TwinoResultCode.LimitExceeded));
            }
        }

        /// <summary>
        /// Pushes clones of the message to cc channel queues
        /// </summary>
        private async Task PushOtherQueues(MqClient client, TwinoMessage clone, List<string> ccList, List<KeyValuePair<string, string>> additionalHeaders)
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

                TwinoQueue queue = await FindQueue(null, queueName, clone);
                if (queue == null)
                    continue;

                TwinoMessage msg = clone;
                if (i < ccList.Count - 1)
                    clone = clone.Clone(false, true, _server.MessageIdGenerator.Create(), additionalHeaders);

                if (!string.IsNullOrEmpty(messageId))
                    msg.SetMessageId(messageId);

                _ = HandlePush(client, msg, queue, false);
            }
        }
    }
}