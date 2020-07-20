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
        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ConsumerReceiveFailed(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing in this implementation
        /// </summary>
        public async Task MessageDequeued(ChannelQueue queue, QueueMessage message)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing for this implementation and returns false
        /// </summary>
        public async Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            return await Task.FromResult(false);
        }
    }
}