using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Dynamic Queue message binding.
    /// Targets queues.
    /// Creates queue automatically from message headers, if it's not exist.
    /// Queue is searched for each published messages. This binding can send each message to different queues.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class DynamicQueueBinding : Binding
    {
        /// <summary>
        /// Creates new queue binding.
        /// Name is the name of the binding.
        /// Priority for router binding.
        /// </summary>
        public DynamicQueueBinding(string name, int priority, BindingInteraction interaction)
            : base(name, null, null, priority, interaction)
        {
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public override async Task<bool> Send(MqClient sender, TwinoMessage message)
        {
            try
            {
                TwinoQueue queue = await GetQueue(message);
                if (queue == null)
                    return false;

                string messageId = Interaction == BindingInteraction.None
                                       ? Router.Server.MessageIdGenerator.Create()
                                       : message.MessageId;

                TwinoMessage msg = message.Clone(true, true, messageId);

                msg.Type = MessageType.QueueMessage;
                msg.SetTarget(Target);
                msg.WaitResponse = Interaction == BindingInteraction.Response;

                QueueMessage queueMessage = new QueueMessage(msg);
                queueMessage.Source = sender;

                PushResult result = await queue.Push(queueMessage, sender);
                return result == PushResult.Success;
            }
            catch (Exception e)
            {
                Router.Server.SendError("BINDING_SEND", e, $"Type:AutoQueue, Binding:{Name}");
                return false;
            }
        }

        /// <summary>
        /// Gets queue.
        /// If it's not cached, finds and caches it before returns.
        /// </summary>
        /// <returns></returns>
        private async Task<TwinoQueue> GetQueue(TwinoMessage message)
        {
            string queueName = message.FindHeader(TwinoHeaders.QUEUE_NAME);
            if (queueName == null)
                return null;

            TwinoQueue queue = await Router.Server.CreateQueue(queueName, Router.Server.Options, message, null, true, true);
            return queue;
        }
    }
}