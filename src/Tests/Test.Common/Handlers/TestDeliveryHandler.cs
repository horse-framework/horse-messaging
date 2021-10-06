using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Test.Common.Handlers
{
    public class TestDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly TestHorseRider _rider;

        public TestDeliveryHandler(TestHorseRider rider)
        {
            _rider = rider;
        }

        public Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            _rider.OnReceived++;

            if (_rider.SendAcknowledgeFromMQ)
                return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            _rider.OnSendStarting++;
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            _rider.OnBeforeSend++;
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            _rider.OnAfterSend++;
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            _rider.OnSendCompleted++;
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            _rider.OnAcknowledge++;

            if (!success)
            {
                if (_rider.PutBack == PutBackDecision.No)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));

                return Task.FromResult(Decision.PutBackMessage(_rider.PutBack == PutBackDecision.Regular, DecisionTransmission.Failed));
            }

            return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));
        }

        public Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            _rider.OnTimeUp++;
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            _rider.OnAcknowledgeTimeUp++;
            
            if (_rider.PutBack == PutBackDecision.No)
                return Task.FromResult(Decision.NoveNext());
            
            return Task.FromResult(Decision.PutBackMessage(_rider.PutBack == PutBackDecision.Regular));
        }

        public Task MessageDequeued(HorseQueue queue, QueueMessage message)
        {
            _rider.OnRemove++;
            return Task.CompletedTask;
        }

        public Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception)
        {
            _rider.OnException++;
            return Task.FromResult(Decision.NoveNext());
        }

        public Task<bool> SaveMessage(HorseQueue queue, QueueMessage message)
        {
            _rider.SaveMessage++;
            return Task.FromResult(true);
        }
    }
}