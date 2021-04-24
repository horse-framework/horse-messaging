namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Channel options
    /// </summary>
    public class ChannelOptions
    {
        /// <summary>
        /// If true, the channel is destroyed automatically when channel is inactive (no subscribers, no new published messages).
        /// Set null for server defaults. 
        /// </summary>
        public bool? AutoDestroy { get; set; }
    }
}