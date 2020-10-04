namespace Twino.MQ
{
    /// <summary>
    /// Subscribing to queue result
    /// </summary>
    public enum QueueSubscriptionResult
    {
        /// <summary>
        /// Client has subscribed to the queue
        /// </summary>
        Success,

        /// <summary>
        /// Unauthorized client
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Queue is full
        /// </summary>
        Full
    }
}