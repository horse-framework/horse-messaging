namespace Horse.Messaging.Server.Queues.Sync
{
    /// <summary>
    /// Queue Sync statuses
    /// </summary>
    public enum QueueSyncStatus
    {
        /// <summary>
        /// Queue is not sync status currently
        /// </summary>
        None,
        
        /// <summary>
        /// Node is main, queue is locked and sharing messages with other nodes.
        /// </summary>
        Sharing,
        
        /// <summary>
        /// Node is replica, received messages from main node.
        /// </summary>
        Receiving
    }
}