namespace Twino.MQ
{
    /// <summary>
    /// Joining channel result
    /// </summary>
    public enum ClientJoinResult
    {
        /// <summary>
        /// Client has joined to channel
        /// </summary>
        Success,

        /// <summary>
        /// Unauthorized client
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Channel is full
        /// </summary>
        Full
    }
}