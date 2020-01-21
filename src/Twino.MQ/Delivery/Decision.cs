namespace Twino.MQ.Delivery
{
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
        /// Creates new decision without keeping messages and acknowledge
        /// </summary>
        public Decision(bool allow, bool save)
        {
            Allow = allow;
            SaveMessage = save;
            KeepMessage = false;
            SendAcknowledge = DeliveryAcknowledgeDecision.None;
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