using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;

namespace Test.Common.Handlers
{
    public class TestMessageDeliveryHandler : MemoryDeliveryHandler
    {
        private readonly TestHorseRider _rider;

        public TestMessageDeliveryHandler(TestHorseRider rider, IHorseQueueManager manager, CommitWhen commitWhen, PutBackDecision putBack)
            : base(manager, commitWhen, putBack)
        {
            _rider = rider;
        }

        public override Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            _rider.OnReceived++;

            if (_rider.SendAcknowledgeFromMQ)
                return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

            return base.ReceivedFromProducer(queue, message, sender);
        }

        public override Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            _rider.OnSendStarting++;
            return base.BeginSend(queue, message);
        }

        public override Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            _rider.OnBeforeSend++;
            return base.CanConsumerReceive(queue, message, receiver);
        }

        public override Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            _rider.OnSendCompleted++;
            return base.EndSend(queue, message);
        }

        public override Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            _rider.OnAcknowledge++;

            if (!success)
            {
                if (_rider.PutBack == PutBackDecision.No)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));

                return Task.FromResult(Decision.PutBackMessage(_rider.PutBack == PutBackDecision.Regular, DecisionTransmission.Failed));
            }

            return base.AcknowledgeReceived(queue, acknowledgeMessage, delivery, success);
        }

        public override Task<Decision> AcknowledgeTimeout(HorseQueue queue, MessageDelivery delivery)
        {
            _rider.OnAcknowledgeTimeUp++;
            
            if (_rider.PutBack == PutBackDecision.No)
                return Task.FromResult(Decision.NoveNext());
            
            return base.AcknowledgeTimeout(queue, delivery);
        }
    }
}