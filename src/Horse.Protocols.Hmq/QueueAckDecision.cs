namespace Horse.Protocols.Hmq
{
    /// <summary>
    /// Queue message acknowledge decisions
    /// </summary>
    public enum QueueAckDecision
    {
        /// <summary>
        /// Queue does not care acknowledge messages and does not request acknowledge from consumers
        /// </summary>
        None,

        /// <summary>
        /// Queue requests acknowledge for the message and keeps sending next messages to consumers before acknowledge received
        /// </summary>
        JustRequest,

        /// <summary>
        /// Queue waits for acknowledge before send next message to next consumer
        /// </summary>
        WaitForAcknowledge
    }
}