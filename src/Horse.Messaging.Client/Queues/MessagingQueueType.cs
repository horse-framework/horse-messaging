namespace Horse.Messaging.Client.Queues
{
    /// <summary>
    /// Queue type
    /// </summary>
    public enum MessagingQueueType
    {
        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push the message into the queue and consumer receive when message is pushed
        /// </summary>
        Push,

        /// <summary>
        /// Load balancing status. Queue messaging is in running state.
        /// Producers push the message into the queue and consumer receive when message is pushed.
        /// If there are no available consumers, message will be kept in queue like push status.
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push message into queue, consumers receive the messages when they requested.
        /// Each message is sent only one-receiver at same time.
        /// Request operation removes the message from the queue.
        /// </summary>
        Pull
    }
}