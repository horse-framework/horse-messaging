namespace Twino.MQ.Delivery
{
    /// <summary>
    /// When a node sends a decision to other nodes.
    /// The decision information serializes/deserializes to this model
    /// </summary>
    internal class DecisionOverNode
    {
        /// <summary>
        /// Queue
        /// </summary>
        public string Queue { get; set; }

        /// <summary>
        /// Message Id
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// If true, operation will continue
        /// </summary>
        public bool Allow { get; set; }

        /// <summary>
        /// If true, message will be saved.
        /// If message already saved, second save will be discarded.
        /// </summary>
        public bool SaveMessage { get; set; }

        /// <summary>
        /// If true, message will be kept in front of the queue.
        /// Settings this value always true may cause infinity same message send operation.
        /// </summary>
        public PutBackDecision PutBack { get; set; }

        /// <summary>
        /// If true, server will send an acknowledge message to producer.
        /// Sometimes acknowledge is required after save operation instead of receiving ack from consumer.
        /// This can be true in similar cases.
        /// </summary>
        public DeliveryAcknowledgeDecision Acknowledge { get; set; }
    }
}