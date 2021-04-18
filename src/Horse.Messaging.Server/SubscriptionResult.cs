namespace Horse.Messaging.Server
{
    /// <summary>
    /// Subscribing to queue or channel result
    /// </summary>
    public enum SubscriptionResult
    {
        /// <summary>
        /// Client has subscribed to queue or channel
        /// </summary>
        Success,

        /// <summary>
        /// Unauthorized client
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Queue or channel is full
        /// </summary>
        Full
    }
}