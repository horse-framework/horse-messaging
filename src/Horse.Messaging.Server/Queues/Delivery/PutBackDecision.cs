namespace Horse.Messaging.Server.Queues.Delivery
{
    /// <summary>
    /// Putting message back to the queue decision
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
}