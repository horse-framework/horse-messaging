using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

namespace Twino.MQ.Delivery
{
    /// <summary>
    /// 
    /// </summary>
    public enum PutBackDecision
    {
        /// <summary>
        /// Message will not keep and put back to the queue
        /// </summary>
        No,

        /// <summary>
        /// Message will be put back to the beginning of the queue.
        /// It will be consumed at first.
        /// </summary>
        Start,

        /// <summary>
        /// Message will be put back to the end of the queue.
        /// It will be consumed at last.
        /// </summary>
        End
    }

    /// <summary>
    /// When sending ack to producer is decided.
    /// An acknowledge message is sent to producer.
    /// This handler is called after acknowledge send operation.
    /// </summary>
    public delegate Task QueueAcknowledgeDeliveryHandler(QueueMessage message, MqClient producer, bool success);

    /// <summary>
    /// Decision description for each step in message delivery
    /// </summary>
    public readonly struct Decision
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
        public readonly PutBackDecision PutBack;

        /// <summary>
        /// If true, server will send an acknowledge message to producer.
        /// Sometimes acknowledge is required after save operation instead of receiving ack from consumer.
        /// This can be true in similar cases.
        /// </summary>
        public readonly DeliveryAcknowledgeDecision Acknowledge;

        /// <summary>
        /// If acknowledge is decided, this method will be called after acknowledge sent or failed
        /// </summary>
        public readonly QueueAcknowledgeDeliveryHandler AcknowledgeDelivery;

        /// <summary>
        /// Creates new decision without keeping messages and acknowledge
        /// </summary>
        public Decision(bool allow, bool save)
        {
            Allow = allow;
            SaveMessage = save;
            PutBack = PutBackDecision.No;
            Acknowledge = DeliveryAcknowledgeDecision.None;
            AcknowledgeDelivery = null;
        }

        /// <summary>
        /// Creates new decision with full parameters
        /// </summary>
        public Decision(bool allow, bool save, PutBackDecision putBack, DeliveryAcknowledgeDecision ack)
        {
            Allow = allow;
            SaveMessage = save;
            PutBack = putBack;
            Acknowledge = ack;
            AcknowledgeDelivery = null;
        }

        /// <summary>
        /// Creates new decision with full parameters
        /// </summary>
        public Decision(bool allow, bool save, PutBackDecision putBack, DeliveryAcknowledgeDecision ack, QueueAcknowledgeDeliveryHandler acknowledgeDelivery)
        {
            Allow = allow;
            SaveMessage = save;
            PutBack = putBack;
            Acknowledge = ack;
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