namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// Channel status
    /// </summary>
    public enum ChannelStatus
    {
        /// <summary>
        /// Channel is paused.
        /// New messages are not accepted.
        /// </summary>
        Paused,

        /// <summary>
        /// Channel is running, receiving and sending messages.
        /// </summary>
        Running,

        /// <summary>
        /// Channel is destroyed
        /// </summary>
        Destroyed
    }
}