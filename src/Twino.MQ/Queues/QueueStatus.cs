namespace Twino.MQ.Queues
{
    /// <summary>
    /// Queue status
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Queue messaging is in running state.
        /// Messages are not queued, producers push the message and if there are available consumers, message is sent to them.
        /// Otherwise, message is deleted.
        /// If you need to keep messages and transmit only live messages, Route is good status to consume less resource.
        /// </summary>
        Route,

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
        Pull,

        /// <summary>
        /// Queue messaging is in running state.
        /// Producers push message into queue, consumers receive the messages when they requested.
        /// Only one message can store in queue at same time.
        /// </summary>
        Cache,

        /// <summary>
        /// Queue messages are accepted from producers but they are not sending to consumers even they request new messages. 
        /// </summary>
        Paused,

        /// <summary>
        /// Queue messages are removed, producers can't push any message to the queue and consumers can't receive any message
        /// </summary>
        Stopped
    }
}