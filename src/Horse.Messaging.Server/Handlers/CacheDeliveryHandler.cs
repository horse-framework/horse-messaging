using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Handlers
{
    /// <summary>
    /// Default delivery handler for cache status
    /// </summary>
    public class CacheDeliveryHandler : IMessageDeliveryHandler
    {
        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing in this implementation
        /// </summary>
        public async Task MessageDequeued(HorseQueue queue, QueueMessage message)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Decision: Allow and Keep
        /// </summary>
        public async Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception)
        {
            return await Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing for this implementation and returns false
        /// </summary>
        public async Task<bool> SaveMessage(HorseQueue queue, QueueMessage message)
        {
            return await Task.FromResult(false);
        }
    }
}