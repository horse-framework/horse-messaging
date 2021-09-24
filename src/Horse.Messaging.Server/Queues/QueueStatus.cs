namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Queue status
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Queue is not initialized
        /// </summary>
        NotInitialized,

        /// <summary>
        /// Queue initialized and running
        /// </summary>
        Running,

        /// <summary>
        /// Queue allows to push but messages are not consumed
        /// </summary>
        OnlyPush,

        /// <summary>
        /// Queue allows to consume but new messages are not allowed
        /// </summary>
        OnlyConsume,

        /// <summary>
        /// All push and consume operations are paused
        /// </summary>
        Paused,
        
        /// <summary>
        /// Queue messages are being synced
        /// </summary>
        Syncing
    }
}