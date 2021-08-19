namespace Horse.Messaging.Server.Options
{
    /// <summary>
    /// Auto queue destroy options
    /// </summary>
    public enum QueueDestroy
    {
        /// <summary>
        /// Auto queue destroy is disabled
        /// </summary>
        Disabled,

        /// <summary>
        /// Queue is destroyed when it's empty
        /// </summary>
        NoMessages,

        /// <summary>
        /// Queue is destroyed when there is no consumers
        /// </summary>
        NoConsumers,

        /// <summary>
        /// Queue is destroyed when it's empty and there is no consumers
        /// </summary>
        Empty
    }
}