using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues.Managers
{
    public class MemoryDeliveryHandler : IQueueDeliveryHandler
    {
        public IHorseQueueManager Manager { get; }
        public IDeliveryTracker Tracker { get; }

        public MemoryDeliveryHandler(IHorseQueueManager manager)
        {
            Manager = manager;
            Tracker = new DefaultDeliveryTracker(manager);
        }

        public virtual Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (Manager.Queue.Options.CommitWhen == CommitWhen.AfterReceived)
                return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

            return Task.FromResult(Decision.NoveNext());
        }

        public virtual Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        public virtual Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            return Task.FromResult(true);
        }

        public virtual Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        public virtual Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            if (Manager.Queue.Options.CommitWhen == CommitWhen.AfterSent)
                return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

            if (queue.Options.Acknowledge == QueueAckDecision.None)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.NoveNext());
        }

        public virtual Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            DecisionTransmission transmission = DecisionTransmission.None;
            if (Manager.Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge)
                transmission = success ? DecisionTransmission.Commit : DecisionTransmission.Failed;

            PutBackDecision putBack = PutBackDecision.No;
            if (!success)
                putBack = Manager.Queue.Options.PutBack;

            if (success || putBack == PutBackDecision.No)
                return Task.FromResult(Decision.DeleteMessage(transmission));

            return Task.FromResult(Decision.PutBackMessage(putBack == PutBackDecision.Regular));
        }

        public virtual Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery)
        {
            if (Manager.Queue.Options.PutBack == PutBackDecision.No)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.PutBackMessage(Manager.Queue.Options.PutBack == PutBackDecision.Regular));
        }
    }
}