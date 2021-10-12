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
        public CommitWhen CommitWhen { get; set; }
        public PutBackDecision PutBack { get; set; }

        private readonly PersistentQueueManager _manager;

        public PersistentMessageDeliveryHandler(PersistentQueueManager manager)
        {
            CommitWhen = manager.CommitWhen;
            PutBack = manager.AckTimeoutPutBack;
            
            _manager = manager;
            Tracker = new DefaultDeliveryTracker(manager);
        }

        public async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (_manager.CommitWhen == CommitWhen.AfterSaved)
            {
                bool saved = await _manager.SaveMessage(message);
                if (!saved)
                {
                    return Decision.InterruptFlow(false, DecisionTransmission.Failed);
                }

                return Decision.TransmitToProducer(DecisionTransmission.Commit);
            }

            return Decision.SaveMessage(_manager.CommitWhen == CommitWhen.AfterReceived
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

        public  Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return Task.FromResult(Decision.PutBackMessage(false));

            if (_manager.DeleteWhen == DeleteWhen.AfterSend)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (success)
            {
                if (_manager.DeleteWhen == DeleteWhen.AfterAcknowledge)
                {
                    return Task.FromResult(Decision.DeleteMessage(_manager.CommitWhen == CommitWhen.AfterAcknowledge
                                                                      ? DecisionTransmission.Commit
                                                                      : DecisionTransmission.None));
                }

                if (_manager.CommitWhen == CommitWhen.AfterAcknowledge)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

                return Task.FromResult(Decision.NoveNext());
            }

            if (_manager.CommitWhen == CommitWhen.AfterAcknowledge)
            {
                if (_manager.NegativeAckPutBack == PutBackDecision.No)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));

                return Task.FromResult(Decision.PutBackMessage(_manager.NegativeAckPutBack == PutBackDecision.Regular,
                                                               DecisionTransmission.Failed));
            }


            if (_manager.NegativeAckPutBack == PutBackDecision.No)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.PutBackMessage(_manager.NegativeAckPutBack == PutBackDecision.Regular));
        }

        public Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery)
        {
            if (_manager.AckTimeoutPutBack == PutBackDecision.No)
            {
                QueueMessage queueMessage = delivery.Message;

                if (!queueMessage.IsRemoved)
                {
                    return Task.FromResult(Decision.DeleteMessage(_manager.CommitWhen == CommitWhen.AfterAcknowledge
                                                                      ? DecisionTransmission.Failed
                                                                      : DecisionTransmission.None));
                }

                if (_manager.CommitWhen == CommitWhen.AfterAcknowledge)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));

                return Task.FromResult(Decision.NoveNext());
            }
            else
            {
                return Task.FromResult(Decision.PutBackMessage(_manager.AckTimeoutPutBack == PutBackDecision.Regular,
                                                               _manager.CommitWhen == CommitWhen.AfterAcknowledge
                                                                   ? DecisionTransmission.Failed
                                                                   : DecisionTransmission.None));
            }
        }
    }
}