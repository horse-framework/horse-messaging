namespace Horse.Mq.Delivery
{
    /// <summary>
    /// Sendin Acknowledge decision for delivery decision
    /// </summary>
    public enum DeliveryAcknowledgeDecision : byte
    {
        /// <summary>
        /// Acknowledge message is not sent
        /// </summary>
        None,

        /// <summary>
        /// Acknowledge message is sent, even message is not saved or failed 
        /// </summary>
        Always,

        /// <summary>
        /// Acknowledge message is sent only if message save is successful
        /// </summary>
        IfSaved,
        
        /// <summary>
        /// Acknowledge is sent as negative
        /// </summary>
        Negative,
    }
}