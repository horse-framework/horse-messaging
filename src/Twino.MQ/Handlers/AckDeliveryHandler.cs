using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Handlers
{
    /// <summary>
    /// Enum for decision when ack will sent to producer
    /// </summary>
    public enum AcknowledgeWhen
    {
        /// <summary>
        /// After producer sent message and server received it
        /// </summary>
        AfterReceived,

        /// <summary>
        /// After message is sent to all consumers
        /// </summary>
        AfterSent,

        /// <summary>
        /// After each consumer sent acknowledge message
        /// </summary>
        AfterAcknowledge
    }

    /// <summary>
    /// Quick IMessageDeliveryHandler implementation.
    /// Allows all operations, does not keep and sends acknowledge message to producer
    /// </summary>
    public class AckDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly AcknowledgeWhen _producerAck;
        private readonly PutBackDecision _consumerAckFail;

        /// <summary>
        /// Quick IMessageDeliveryHandler implementation with acknowledge features.
        /// </summary>
        /// <param name="producerAck">Decision, when producer will receive acknowledge (or confirm)</param>
        /// <param name="consumerAckFail">Decision, what will be done if consumer sends nack or doesn't send ack in time</param>
        public AckDeliveryHandler(AcknowledgeWhen producerAck, PutBackDecision consumerAckFail)
        {
            _producerAck = producerAck;
            _consumerAckFail = consumerAckFail;
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterReceived, acknowledge is sent to producer.
        /// </summary>
        public async Task<Decision> ReceivedFromProducer(TwinoQueue queue, QueueMessage message, MqClient sender)
        {
            if (_producerAck == AcknowledgeWhen.AfterReceived)
                return await Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));

            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> BeginSend(TwinoQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> CanConsumerReceive(TwinoQueue queue, QueueMessage message, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> ConsumerReceived(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> ConsumerReceiveFailed(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterSent, acknowledge is sent to producer.
        /// </summary>
        public async Task<Decision> EndSend(TwinoQueue queue, QueueMessage message)
        {
            if (_producerAck == AcknowledgeWhen.AfterSent)
                return await Task.FromResult(new Decision(true, false, PutBackDecision.No, DeliveryAcknowledgeDecision.Always));

            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterAcknowledge, acknowledge is sent to producer.
        /// </summary>
        public async Task<Decision> AcknowledgeReceived(TwinoQueue queue, TwinoMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            DeliveryAcknowledgeDecision ack = DeliveryAcknowledgeDecision.None;

            if (_producerAck == AcknowledgeWhen.AfterAcknowledge)
                ack = success ? DeliveryAcknowledgeDecision.Always : DeliveryAcknowledgeDecision.Negative;

            PutBackDecision putBack = PutBackDecision.No;
            if (!success)
                putBack = _consumerAckFail;

            return await Task.FromResult(new Decision(true, false, putBack, ack));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> MessageTimedOut(TwinoQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<Decision> AcknowledgeTimedOut(TwinoQueue queue, MessageDelivery delivery)
        {
            return await Task.FromResult(new Decision(true, false, _consumerAckFail, DeliveryAcknowledgeDecision.None));
        }

        /// <summary>
        /// Does nothing in this implementation
        /// </summary>
        public async Task MessageDequeued(TwinoQueue queue, QueueMessage message)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> ExceptionThrown(TwinoQueue queue, QueueMessage message, Exception exception)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Does nothing for this implementation and returns false
        /// </summary>
        public async Task<bool> SaveMessage(TwinoQueue queue, QueueMessage message)
        {
            return await Task.FromResult(false);
        }
    }
}