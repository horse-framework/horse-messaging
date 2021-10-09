namespace Horse.Messaging.Server.Queues.Delivery
{
    /// <summary>
    /// Enum for decision when commit will sent to producer
    /// </summary>
    public enum CommitWhen
    {
        /// <summary>
        /// Commit message is not sent
        /// </summary>
        None,
        
        /// <summary>
        /// After producer sent message and server received it
        /// </summary>
        AfterReceived,
        
        /// <summary>
        /// After producer sent message and server saves it to disk
        /// </summary>
        AfterSaved,

        /// <summary>
        /// After message is sent to all consumers
        /// </summary>
        AfterSent,

        /// <summary>
        /// After each consumer sent acknowledge message
        /// </summary>
        AfterAcknowledge
    }
}