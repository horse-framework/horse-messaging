using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Protocol;
using Horse.Messaging.Server.Queues.Delivery;

namespace Test.Common.Handlers
{
    public class TestDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly TestHorseMq _mq;

        public TestDeliveryHandler(TestHorseMq mq)
        {
            _mq = mq;
        }

        public Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            _mq.OnReceived++;

            if (_mq.SendAcknowledgeFromMQ)
                return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));

            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            _mq.OnSendStarting++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            _mq.OnBeforeSend++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            _mq.OnAfterSend++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            _mq.OnSendCompleted++;
            return Task.FromResult(new Decision(true, true));
        }

        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            _mq.OnAcknowledge++;

            if (!success)
                return Task.FromResult(new Decision(true, false, _mq.PutBack, DeliveryAcknowledgeDecision.Always));
            
            return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));
        }

        public Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            _mq.OnTimeUp++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            _mq.OnAcknowledgeTimeUp++;
            return Task.FromResult(new Decision(true, false, _mq.PutBack, DeliveryAcknowledgeDecision.None));
        }

        public Task MessageDequeued(HorseQueue queue, QueueMessage message)
        {
            _mq.OnRemove++;
            return Task.CompletedTask;
        }

        public Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception)
        {
            _mq.OnException++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<bool> SaveMessage(HorseQueue queue, QueueMessage message)
        {
            _mq.SaveMessage++;
            return Task.FromResult(true);
        }
    }
}