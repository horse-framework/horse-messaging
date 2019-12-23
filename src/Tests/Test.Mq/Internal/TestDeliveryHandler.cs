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

        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            _server.OnReceived++;
            
            if (_server.SendAcknowledgeFromMQ)
                return await Task.FromResult(new Decision(true, false, false, DeliveryAcknowledgeDecision.Always));
            
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            _server.OnSendStarting++;
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<bool> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            _server.OnBeforeSend++;
            return await Task.FromResult(true);
        }

        public async Task ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            _server.OnAfterSend++;
            await Task.CompletedTask;
        }

        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            _server.OnSendCompleted++;
            return await Task.FromResult(new Decision(true, true));
        }

        public async Task AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            _server.OnAcknowledge++;
            await Task.CompletedTask;
        }

        public async Task MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            _server.OnTimeUp++;
            await Task.CompletedTask;
        }

        public async Task AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            _server.OnAcknowledgeTimeUp++;
            await Task.CompletedTask;
        }

        public async Task MessageRemoved(ChannelQueue queue, QueueMessage message)
        {
            _server.OnRemove++;
            await Task.CompletedTask;
        }

        public async Task ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            _server.OnException++;
            await Task.CompletedTask;
        }

        public async Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            _server.SaveMessage++;
            return await Task.FromResult(true);
        }
    }
}