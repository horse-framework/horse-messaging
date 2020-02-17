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
    public class SendAckDeliveryHandler : IMessageDeliveryHandler
    {
        private readonly AcknowledgeWhen _when;

        /// <summary>
        /// Quick IMessageDeliveryHandler implementation.
        /// Allows all operations, does not keep and sends acknowledge message to producer
        /// </summary>
        public SendAckDeliveryHandler(AcknowledgeWhen when)
        {
            _when = when;
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterReceived, acknowledge is sent to producer.
        /// </summary>
        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            if (_when == AcknowledgeWhen.AfterReceived)
                return await Task.FromResult(new Decision(true, false, false, DeliveryAcknowledgeDecision.Always));

            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> ConsumerReceiveFailed(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterSent, acknowledge is sent to producer.
        /// </summary>
        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            if (_when == AcknowledgeWhen.AfterSent)
                return await Task.FromResult(new Decision(true, false, false, DeliveryAcknowledgeDecision.Always));

            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow.
        /// If AcknowledgeWhen is AfterAcknowledge, acknowledge is sent to producer.
        /// </summary>
        public async Task<Decision> AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (_when == AcknowledgeWhen.AfterAcknowledge)
                return await Task.FromResult(new Decision(true, false, false, DeliveryAcknowledgeDecision.Always));

            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task<Decision> AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            return await Task.FromResult(new Decision(true, false));
        }

        /// <summary>
        /// Does nothing in this implementation
        /// </summary>
        public async Task MessageRemoved(ChannelQueue queue, QueueMessage message)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Decision: Allow
        /// </summary>
        public async Task<Decision> ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            return await Task.FromResult(new Decision(true, false));
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