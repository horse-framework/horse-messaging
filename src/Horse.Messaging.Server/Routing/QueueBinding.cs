using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// Queue message binding.
    /// Targets queues.
    /// Binding receivers are received messages as QueueMessage.
    /// </summary>
    public class QueueBinding : Binding
    {
        private HorseQueue _targetQueue;
        private DateTime _queueUpdateTime;

        /// <summary>
        /// Creates new queue binding.
        /// Name is the name of the binding.
        /// Target should be queue name.
        /// Content Type should be Queue Id.
        /// Priority for router binding.
        /// </summary>
        public QueueBinding(string name, string target, int priority, BindingInteraction interaction)
            : base(name, target, null, priority, interaction)
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
                Router.Rider.SendError("BINDING_SEND", e, $"Type:Queue, Binding:{Name}");
                return false;
            }
        }

        /// <summary>
        /// Gets queue.
        /// If it's not cached, finds and caches it before returns.
        /// </summary>
        /// <returns></returns>
        private async Task<HorseQueue> GetQueue(HorseMessage requestMessage)
        {
            if (_targetQueue != null && DateTime.UtcNow - _queueUpdateTime < TimeSpan.FromMinutes(1))
                return _targetQueue;

            HorseQueue queue = Router.Rider.Queue.Find(Target);

            if (queue == null)
            {
                if (Router.Rider.Options.AutoQueueCreation)
                    queue = await Router.Rider.Queue.Create(Target, Router.Rider.Queue.Options, requestMessage, Router.Rider.Queue.DeliveryHandlerFactory, true, true);
            }

            if (queue != null)
            {
                _queueUpdateTime = DateTime.UtcNow;
                _targetQueue = queue;
            }

            return _targetQueue;
        }
    }
}