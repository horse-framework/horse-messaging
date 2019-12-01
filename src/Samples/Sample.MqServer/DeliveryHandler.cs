using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Sample.MqServer
{
    public class DeliveryHandler : IMessageDeliveryHandler
    {
        public Task<MessageDecision> OnReceived(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            throw new NotImplementedException();
        }

        public Task<MessageDecision> OnSendStarting(ChannelQueue queue, QueueMessage message)
        {
            throw new NotImplementedException();
        }

        public Task<DeliveryDecision> OnBeforeSend(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            throw new NotImplementedException();
        }

        public Task OnAfterSend(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            throw new NotImplementedException();
        }

        public Task<DeliveryOperation> OnSendCompleted(ChannelQueue queue, QueueMessage message)
        {
            throw new NotImplementedException();
        }

        public Task OnAcknowledge(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            throw new NotImplementedException();
        }

        public Task OnTimeUp(ChannelQueue queue, QueueMessage message)
        {
            throw new NotImplementedException();
        }

        public Task OnAcknowledgeTimeUp(ChannelQueue queue, MessageDelivery delivery)
        {
            throw new NotImplementedException();
        }

        public Task OnRemove(ChannelQueue queue, QueueMessage message)
        {
            throw new NotImplementedException();
        }

        public Task OnException(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            throw new NotImplementedException();
        }
    }
}