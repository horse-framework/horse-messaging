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
                return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));

            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            _rider.OnSendStarting++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            _rider.OnBeforeSend++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            _rider.OnAfterSend++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            _rider.OnSendCompleted++;
            return Task.FromResult(new Decision(true, true));
        }

        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            _rider.OnAcknowledge++;

            if (!success)
                return Task.FromResult(new Decision(true, false, _rider.PutBack, DeliveryAcknowledgeDecision.Always));

            return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));
        }

        public Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            _rider.OnTimeUp++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            _rider.OnAcknowledgeTimeUp++;
            return Task.FromResult(new Decision(true, false, _rider.PutBack, DeliveryAcknowledgeDecision.None));
        }

        public Task MessageDequeued(HorseQueue queue, QueueMessage message)
        {
            _rider.OnRemove++;
            return Task.CompletedTask;
        }

        public Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception)
        {
            _rider.OnException++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<bool> SaveMessage(HorseQueue queue, QueueMessage message)
        {
            _rider.SaveMessage++;
            return Task.FromResult(true);
        }
    }
}