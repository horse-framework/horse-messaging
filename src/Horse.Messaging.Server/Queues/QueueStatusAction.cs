namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Queue status change decision enum
    /// </summary>
    public enum QueueStatusAction
    {
        /// <summary>
        /// Denies the operation
        /// </summary>
        Deny,

        /// <summary>
        /// Allows the operation
        /// </summary>
        Allow,

        /// <summary>
        /// Denies the operation and calls trigger method in previos status
        /// </summary>
        DenyAndTrigger,

        /// <summary>
        /// Allows the operation and calls trigger method in next status
        /// </summary>
        AllowAndTrigger
    }
}