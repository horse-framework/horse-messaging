using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Test.Common.Handlers
{
    public class TestDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly TestTwinoMQ _mq;

        public TestDeliveryHandler(TestTwinoMQ mq)
        {
            _mq = mq;
        }

        public Task<Decision> ReceivedFromProducer(TwinoQueue queue, QueueMessage message, MqClient sender)
        {
            _mq.OnReceived++;

            if (_mq.SendAcknowledgeFromMQ)
                return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));

            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> BeginSend(TwinoQueue queue, QueueMessage message)
        {
            _mq.OnSendStarting++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> CanConsumerReceive(TwinoQueue queue, QueueMessage message, MqClient receiver)
        {
            _mq.OnBeforeSend++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceived(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            _mq.OnAfterSend++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> ConsumerReceiveFailed(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> EndSend(TwinoQueue queue, QueueMessage message)
        {
            _mq.OnSendCompleted++;
            return Task.FromResult(new Decision(true, true));
        }

        public Task<Decision> AcknowledgeReceived(TwinoQueue queue, TwinoMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            _mq.OnAcknowledge++;
            return Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));
        }

        public Task<Decision> MessageTimedOut(TwinoQueue queue, QueueMessage message)
        {
            _mq.OnTimeUp++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<Decision> AcknowledgeTimedOut(TwinoQueue queue, MessageDelivery delivery)
        {
            _mq.OnAcknowledgeTimeUp++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task MessageDequeued(TwinoQueue queue, QueueMessage message)
        {
            _mq.OnRemove++;
            return Task.CompletedTask;
        }

        public Task<Decision> ExceptionThrown(TwinoQueue queue, QueueMessage message, Exception exception)
        {
            _mq.OnException++;
            return Task.FromResult(new Decision(true, false));
        }

        public Task<bool> SaveMessage(TwinoQueue queue, QueueMessage message)
        {
            _mq.SaveMessage++;
            return Task.FromResult(true);
        }
    }
}