using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Test.Mq.Internal
{
    public class TestDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly TestMqServer _server;

        public TestDeliveryHandler(TestMqServer server)
        {
            _server = server;
        }

        public async Task<Decision> ReceivedFromProducer(TwinoQueue queue, QueueMessage message, MqClient sender)
        {
            _server.OnReceived++;

            if (_server.SendAcknowledgeFromMQ)
                return await Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));

            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> BeginSend(TwinoQueue queue, QueueMessage message)
        {
            _server.OnSendStarting++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> CanConsumerReceive(TwinoQueue queue, QueueMessage message, MqClient receiver)
        {
            _server.OnBeforeSend++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> ConsumerReceived(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            _server.OnAfterSend++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> ConsumerReceiveFailed(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> EndSend(TwinoQueue queue, QueueMessage message)
        {
            _server.OnSendCompleted++;
            return await Task.FromResult(new Decision(true, true));
        }

        public async Task<Decision> AcknowledgeReceived(TwinoQueue queue, TwinoMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            _server.OnAcknowledge++;
            return await Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));
        }

        public async Task<Decision> MessageTimedOut(TwinoQueue queue, QueueMessage message)
        {
            _server.OnTimeUp++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> AcknowledgeTimedOut(TwinoQueue queue, MessageDelivery delivery)
        {
            _server.OnAcknowledgeTimeUp++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task MessageDequeued(TwinoQueue queue, QueueMessage message)
        {
            _server.OnRemove++;
            await Task.CompletedTask;
        }

        public async Task<Decision> ExceptionThrown(TwinoQueue queue, QueueMessage message, Exception exception)
        {
            _server.OnException++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<bool> SaveMessage(TwinoQueue queue, QueueMessage message)
        {
            _server.SaveMessage++;
            return await Task.FromResult(true);
        }
    }
}