namespace Twino.MQ.Clients
{
    /// <summary>
    /// Client queue subscription request message.
    /// </summary>
    public class ClientSubscriptionInfo
    {
        /// <summary>
        /// Channel name
        /// </summary>
        public string ChannelName { get; set; }
        
        /// <summary>
        /// Queue content types in the channel.
        /// </summary>
        public ushort[] ContentTypes { get; set; }
    }
}