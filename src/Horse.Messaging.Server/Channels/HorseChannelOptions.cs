namespace Horse.Messaging.Server.Channels
{
    /// <summary>
    /// Horse Channel options
    /// </summary>
    public class HorseChannelOptions
    {
        /// <summary>
        /// Maximum message size limit
        /// Zero is unlimited
        /// </summary>
        public ulong MessageSizeLimit { get; set; }

        /// <summary>
        /// Maximum client limit of the queue
        /// Zero is unlimited
        /// </summary>
        public int ClientLimit { get; set; }

        /// <summary>
        /// If true,
        /// Acknowledge message is sent to producer, when message is received.
        /// </summary>
        public bool SendAcknowledge { get; set; }
    }
}