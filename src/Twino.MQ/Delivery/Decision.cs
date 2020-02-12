using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Delivery
{

    /// <summary>
    /// When sending ack to producer is decided.
    /// An acknowledge message is sent to producer.
    /// This handler is called after acknowledge send operation.
    /// </summary>
    public delegate Task QueueAcknowledgeDeliveryHandler(QueueMessage message, MqClient producer, bool success);
    
    /// <summary>
    /// Decision description for each step in message delivery
    /// </summary>
    public struct Decision
    {
        /// <summary>
        /// If true, operation will continue
        /// </summary>
        public readonly bool Allow;

        /// <summary>
        /// If true, message will be saved.
        /// If message already saved, second save will be discarded.
        /// </summary>
        public readonly bool SaveMessage;

        /// <summary>
        /// If true, message will be kept in front of the queue.
        /// Settings this value always true may cause infinity same message send operation.
        /// </summary>
        public readonly bool KeepMessage;

        /// <summary>
        /// If true, server will send an acknowledge message to producer.
        /// Sometimes acknowledge is required after save operation instead of receiving ack from consumer.
        /// This can be true in similar cases.
        /// </summary>
        public readonly DeliveryAcknowledgeDecision SendAcknowledge;

        /// <summary>
        /// If acknowledge is decided, this method will be called after acknowledge sent or failed
        /// </summary>
        public QueueAcknowledgeDeliveryHandler AcknowledgeDelivery;
        
        /// <summary>
        /// Creates new decision without keeping messages and acknowledge
        /// </summary>
        public Decision(bool allow, bool save)
        {
            Allow = allow;
            SaveMessage = save;
            KeepMessage = false;
            SendAcknowledge = DeliveryAcknowledgeDecision.None;
            AcknowledgeDelivery = null;
        }

        /// <summary>
        /// Creates new decision with full parameters
        /// </summary>
        public Decision(bool allow, bool save, bool keep, DeliveryAcknowledgeDecision sendAcknowledge)
        {
            Allow = allow;
            SaveMessage = save;
            KeepMessage = keep;
            SendAcknowledge = sendAcknowledge;
            AcknowledgeDelivery = null;
        }

        /// <summary>
        /// Creates new decision with full parameters
        /// </summary>
        public Decision(bool allow, bool save, bool keep, DeliveryAcknowledgeDecision sendAcknowledge, QueueAcknowledgeDeliveryHandler acknowledgeDelivery)
        {
            Allow = allow;
            SaveMessage = save;
            KeepMessage = keep;
            SendAcknowledge = sendAcknowledge;
            AcknowledgeDelivery = acknowledgeDelivery;
        }
        
        /// <summary>
        /// Creates allow decision.
        /// Value does not save, keep and send acknowledge
        /// </summary>
        /// <returns></returns>
        public static Decision JustAllow()
        {
            return new Decision(true, false);
        }
    }
}