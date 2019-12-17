using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
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

        public async Task<MessageDecision> OnReceived(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            _server.OnReceived++;
            return await Task.FromResult(MessageDecision.Allow);
        }

        public async Task<MessageDecision> OnSendStarting(ChannelQueue queue, QueueMessage message)
        {
            _server.OnSendStarting++;
            return await Task.FromResult(MessageDecision.Allow);
        }

        public async Task<DeliveryDecision> OnBeforeSend(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            _server.OnBeforeSend++;
            return await Task.FromResult(DeliveryDecision.Allow);
        }

        public async Task OnAfterSend(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            _server.OnAfterSend++;
            await Task.FromResult(DeliveryDecision.Allow);
        }

        public async Task<DeliveryOperation> OnSendCompleted(ChannelQueue queue, QueueMessage message)
        {
            _server.OnSendCompleted++;
            return await Task.FromResult(DeliveryOperation.SaveMessage);
        }

        public async Task OnAcknowledge(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            _server.OnAcknowledge++;
            await Task.CompletedTask;
        }

        public async Task OnTimeUp(ChannelQueue queue, QueueMessage message)
        {
            _server.OnTimeUp++;
            await Task.CompletedTask;
        }

        public async Task OnAcknowledgeTimeUp(ChannelQueue queue, MessageDelivery delivery)
        {
            _server.OnAcknowledgeTimeUp++;
            await Task.CompletedTask;
        }

        public async Task OnRemove(ChannelQueue queue, QueueMessage message)
        {
            _server.OnRemove++;
            await Task.CompletedTask;
        }

        public async Task OnException(ChannelQueue queue, QueueMessage message, Exception exception)
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