namespace Twino.MQ.Options
{
    /// <summary>
    /// Channel options
    /// </summary>
    public class ChannelOptions : ChannelQueueOptions
    {
        /// <summary>
        /// If true, channel can have multiple queues with multiple content type.
        /// If false, each channel can only have one queue
        /// </summary>
        public bool AllowMultipleQueues { get; set; } = true;

        /// <summary>
        /// Allowed queue id list for the channel
        /// </summary>
        public ushort[] AllowedQueues { get; set; }
    }
}