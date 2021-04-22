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
        /// If true, the channel is created when a new message is sent to the channel is not created yet.
        /// </summary>
        public bool AutoChannelCreation { get; set; } = true;
        
        /// <summary>
        /// If true, the channel is destroyed automatically in a short period of time after last subscriber left. 
        /// </summary>
        public bool AutoDestroy { get; set; } = true; //todo: feature is not enabled

        /// <summary>
        /// Clones options and return new copy
        /// </summary>
        public static HorseChannelOptions Clone(HorseChannelOptions other)
        {
            return new HorseChannelOptions
            {
                ClientLimit = other.ClientLimit,
                MessageSizeLimit = other.MessageSizeLimit
            };
        }
    }
}