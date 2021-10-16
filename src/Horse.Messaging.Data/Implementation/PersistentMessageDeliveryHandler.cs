using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Data.Implementation
{
    public class PersistentMessageDeliveryHandler : IQueueDeliveryHandler
    {
        public IHorseQueueManager Manager => _manager;
        public IDeliveryTracker Tracker { get; }

        private readonly PersistentQueueManager _manager;

        public PersistentMessageDeliveryHandler(PersistentQueueManager manager)
        {
            _manager = manager;
            Tracker = new DefaultDeliveryTracker(manager);
        }

        public async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (_manager.Queue.Options.CommitWhen == CommitWhen.AfterSaved)
            {
                bool saved = await _manager.SaveMessage(message);
                if (!saved)
                {
                    return Decision.InterruptFlow(false, DecisionTransmission.Failed);
                }

                return Decision.TransmitToProducer(DecisionTransmission.Commit);
            }

            return Decision.SaveMessage(_manager.Queue.Options.CommitWhen == CommitWhen.AfterReceived
                ? DecisionTransmission.Commit
                : DecisionTransmission.None);
        }

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

        public Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            return Task.FromResult(true);
        }

        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.PutBackMessage(false));
        }

        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return Task.FromResult(Decision.PutBackMessage(false));

            if (_manager.Queue.Options.Acknowledge == QueueAckDecision.None)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.NoveNext());
        }

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