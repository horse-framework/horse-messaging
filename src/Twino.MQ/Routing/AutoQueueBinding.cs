using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Routing
{
    /// <summary>
    /// Auto Queue message binding.
    /// Targets queues.
    /// Creates queue automatically from message headers, if it's not exist.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class AutoQueueBinding : Binding
    {
        private TwinoQueue _targetQueue;

        /// <summary>
        /// Creates new queue binding.
        /// Name is the name of the binding.
        /// Priority for router binding.
        /// </summary>
        public AutoQueueBinding(string name, int priority, BindingInteraction interaction)
            : base(name, null, null, priority, interaction)
        {
        }

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

                PushResult result = await _targetQueue.Push(queueMessage, sender);
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
            if (_targetQueue != null)
                return _targetQueue;

            string queueName = message.FindHeader(TwinoHeaders.QUEUE_NAME);
            if (queueName == null)
                return null;

            TwinoQueue queue = Router.Server.FindQueue(queueName);
            if (queue != null)
            {
                _targetQueue = queue;
                return _targetQueue;
            }

            _targetQueue = await Router.Server.CreateQueue(queueName, Router.Server.Options, message, null, true, true);
            return _targetQueue;
        }
    }
}