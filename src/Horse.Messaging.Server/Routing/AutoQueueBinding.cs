using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// Auto Queue message binding.
    /// Targets queues.
    /// Creates queue automatically from message headers, if it's not exist.
    /// Once the queue is created, always same queue is used for all messages.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class AutoQueueBinding : Binding
    {
        private HorseQueue _targetQueue;

        /// <summary>
        /// Creates new queue binding.
        /// Name is the name of the binding.
        /// Priority for router binding.
        /// </summary>
        public AutoQueueBinding(string name, int priority, BindingInteraction interaction)
            : base(name, null, null, priority, interaction)
        {
        }

        /// <summary>
        /// Sends the message to binding receivers
        /// </summary>
        public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
        {
            try
            {
                HorseQueue queue = await GetQueue(message);
                if (queue == null)
                    return false;

                string messageId = Interaction == BindingInteraction.None
                                       ? Router.Rider.MessageIdGenerator.Create()
                                       : message.MessageId;

                HorseMessage msg = message.Clone(true, true, messageId);

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
                Router.Rider.SendError("BINDING_SEND", e, $"Type:AutoQueue, Binding:{Name}");
                return false;
            }
        }

        /// <summary>
        /// Gets queue.
        /// If it's not cached, finds and caches it before returns.
        /// </summary>
        /// <returns></returns>
        private async Task<HorseQueue> GetQueue(HorseMessage message)
        {
            if (_targetQueue != null)
                return _targetQueue;

            string queueName = message.FindHeader(HorseHeaders.QUEUE_NAME);
            if (queueName == null)
                return null;

            HorseQueue queue = Router.Rider.Queue.Find(queueName);
            if (queue != null)
            {
                _targetQueue = queue;
                return _targetQueue;
            }

            _targetQueue = await Router.Rider.Queue.Create(queueName, Router.Rider.Queue.Options, message, Router.Rider.Queue.DeliveryHandlerFactory, true, true);
            return _targetQueue;
        }
    }
}