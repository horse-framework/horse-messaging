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

        /// <summary>
        /// Maximum client limit of the channel.
        /// Zero is unlimited
        /// </summary>
        public int ClientLimit { get; set; }
        
        /// <summary>
        /// Maximum queue limit of the channel.
        /// Zero is unlimited
        /// </summary>
        public int QueueLimit { get; set; }

        /// <summary>
        /// Clones channel options from another options
        /// </summary>
        internal static ChannelOptions CloneFrom(ChannelOptions options)
        {
            return new ChannelOptions
                   {
                       Status = options.Status,
                       AcknowledgeTimeout = options.AcknowledgeTimeout,
                       AllowedQueues = options.AllowedQueues,
                       MessageTimeout = options.MessageTimeout,
                       RequestAcknowledge = options.RequestAcknowledge,
                       AllowMultipleQueues = options.AllowMultipleQueues,
                       HideClientNames = options.HideClientNames,
                       UseMessageId = options.UseMessageId,
                       WaitForAcknowledge = options.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = options.SendOnlyFirstAcquirer,
                       ClientLimit = options.ClientLimit,
                       QueueLimit = options.QueueLimit
                   };
        }
    }
}