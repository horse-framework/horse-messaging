namespace Horse.Messaging.Data
{
    /// <summary>
    /// Acknowledge decision for messages producer sent.
    /// Ack is sent from server to producer.
    /// </summary>
    public enum ProducerAckDecision
    {
        /// <summary>
        /// Acknowledge message is not sent
        /// </summary>
        None,

        /// <summary>
        /// Acknowledge message is sent right after message is received, before save. 
        /// </summary>
        AfterReceived,

        /// <summary>
        /// Acknowledge message is sent right after message is saved
        /// </summary>
        AfterSaved,
        
        /// <summary>
        /// Ack is sent to producer when consumer sends ack to server.
        /// If consumer sends negative ack or ack is timed out, producer receives negative ack.
        /// </summary>
        AfterConsumerAckReceived
    }
}