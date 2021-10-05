using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues.Handlers
{
    /// <summary>
    /// Quick IMessageDeliveryHandler implementation.
    /// Allows all operations, does not keep and does not send acknowledge message to producer
    /// </summary>
    public class JustAllowDeliveryHandler : IMessageDeliveryHandler
    {
        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.DeleteMessage());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Does nothing in this implementation
        /// </summary>
        public async Task MessageDequeued(HorseQueue queue, QueueMessage message)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Does nothing for this implementation and returns false
        /// </summary>
        public Task<bool> SaveMessage(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(false);
        }
    }
}