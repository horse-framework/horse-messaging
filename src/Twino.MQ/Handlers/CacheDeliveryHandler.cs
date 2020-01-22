using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Handlers
{
    /// <summary>
    /// Default delivery handler for cache status
    /// </summary>
    public class CacheDeliveryHandler : IMessageDeliveryHandler
    {
        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> ConsumerReceiveFailed(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing in this implementation
        /// </summary>
        public Task MessageRemoved(ChannelQueue queue, QueueMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public Task<Decision> ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            return Task.FromResult(new Decision(true, false, true, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing for this implementation and returns false
        /// </summary>
        public Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(false);
        }
    }
}