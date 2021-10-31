using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Data.Implementation
{
    /// <summary>
    /// Message delivery handler for persistent queues
    /// </summary>
    public class PersistentMessageDeliveryHandler : IQueueDeliveryHandler
    {
        /// <inheritdoc />
        public IHorseQueueManager Manager => _manager;
        
        /// <inheritdoc />
        public IDeliveryTracker Tracker { get; }

        private readonly PersistentQueueManager _manager;

        /// <summary>
        /// Creates new persistent message delivery handler
        /// </summary>
        public PersistentMessageDeliveryHandler(PersistentQueueManager manager)
        {
            _manager = manager;
            Tracker = new DefaultDeliveryTracker(manager);
        }

        /// <inheritdoc />
        public async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (_manager.Queue.Options.CommitWhen == CommitWhen.AfterSaved)
            {
                message.IsSaved = await _manager.SaveMessage(message);
                
                if (!message.IsSaved)
                {
                    return Decision.InterruptFlow(false, DecisionTransmission.Failed);
                }

                return Decision.TransmitToProducer(DecisionTransmission.Commit);
            }

            return Decision.SaveMessage(_manager.Queue.Options.CommitWhen == CommitWhen.AfterReceived
                ? DecisionTransmission.Commit
                : DecisionTransmission.None);
        }

        /// <inheritdoc />
        public async Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            if (_manager.UseRedelivery)
            {
                message.DeliveryCount++;
                await _manager.RedeliveryService.Set(message.Message.MessageId, message.DeliveryCount);

                if (message.DeliveryCount > 1)
                    message.Message.SetOrAddHeader(HorseHeaders.DELIVERY, message.DeliveryCount.ToString());
            }

            return Decision.NoveNext();
        }

        /// <inheritdoc />
        public Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.PutBackMessage(false));
        }

        /// <inheritdoc />
        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return Task.FromResult(Decision.PutBackMessage(false));

            if (_manager.Queue.Options.Acknowledge == QueueAckDecision.None)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.NoveNext());
        }

        /// <inheritdoc />
        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (success)
            {
                if (_manager.Queue.Options.Acknowledge != QueueAckDecision.None)
                {
                    return Task.FromResult(Decision.DeleteMessage(_manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge
                        ? DecisionTransmission.Commit
                        : DecisionTransmission.None));
                }

                if (_manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

                return Task.FromResult(Decision.NoveNext());
            }

            if (_manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge)
            {
                if (_manager.Queue.Options.PutBack == PutBackDecision.No)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));

                return Task.FromResult(Decision.PutBackMessage(_manager.Queue.Options.PutBack == PutBackDecision.Regular,
                    DecisionTransmission.Failed));
            }


            if (_manager.Queue.Options.PutBack == PutBackDecision.No)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.PutBackMessage(_manager.Queue.Options.PutBack == PutBackDecision.Regular));
        }

        /// <inheritdoc />
        public Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery)
        {
            if (_manager.Queue.Options.PutBack == PutBackDecision.No)
            {
                QueueMessage queueMessage = delivery.Message;

                if (!queueMessage.IsRemoved)
                {
                    return Task.FromResult(Decision.DeleteMessage(_manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge
                        ? DecisionTransmission.Failed
                        : DecisionTransmission.None));
                }

                if (_manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));

                return Task.FromResult(Decision.NoveNext());
            }

            return Task.FromResult(Decision.PutBackMessage(_manager.Queue.Options.PutBack == PutBackDecision.Regular,
                _manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge
                    ? DecisionTransmission.Failed
                    : DecisionTransmission.None));
        }
    }
}