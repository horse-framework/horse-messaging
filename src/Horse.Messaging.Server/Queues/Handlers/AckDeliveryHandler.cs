using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Queues.Handlers
{
    /// <summary>
    /// Quick IMessageDeliveryHandler implementation.
    /// Allows all operations, does not keep and sends acknowledge message to producer
    /// </summary>
    public class AckDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly CommitWhen _producerAck;
        private readonly PutBackDecision _consumerAckFail;

        /// <summary>
        /// Quick IMessageDeliveryHandler implementation with acknowledge features.
        /// </summary>
        /// <param name="producerAck">Decision, when producer will receive acknowledge (or confirm)</param>
        /// <param name="consumerAckFail">Decision, what will be done if consumer sends nack or doesn't send ack in time</param>
        public AckDeliveryHandler(CommitWhen producerAck, PutBackDecision consumerAckFail)
        {
            _producerAck = producerAck;
            _consumerAckFail = consumerAckFail;
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterReceived, acknowledge is sent to producer.
        /// </summary>
        public Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (_producerAck == CommitWhen.AfterReceived)
                return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.NoveNext());
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
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterSent, acknowledge is sent to producer.
        /// </summary>
        public Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            if (_producerAck == CommitWhen.AfterSent)
                return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Commit));

            return Task.FromResult(Decision.NoveNext());
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterAcknowledge, acknowledge is sent to producer.
        /// </summary>
        public Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            DecisionTransmission transmission = DecisionTransmission.None;
            if (_producerAck == CommitWhen.AfterAcknowledge)
                transmission = success ? DecisionTransmission.Commit : DecisionTransmission.Failed;

            PutBackDecision putBack = PutBackDecision.No;
            if (!success)
                putBack = _consumerAckFail;

            if (success || putBack == PutBackDecision.No)
                return Task.FromResult(Decision.DeleteMessage(transmission));

            return Task.FromResult(Decision.PutBackMessage(putBack == PutBackDecision.Regular));
        }

        /// <summary>
        /// 
        /// </summary>
        public Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.DeleteMessage());
        }

        /// <summary>
        /// 
        /// </summary>
        public Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            if (_consumerAckFail == PutBackDecision.No)
                return Task.FromResult(Decision.DeleteMessage());

            return Task.FromResult(Decision.PutBackMessage(_consumerAckFail == PutBackDecision.Regular));
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