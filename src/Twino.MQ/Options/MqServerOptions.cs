namespace Twino.MQ.Options
{
    /// <summary>
    /// Server default options
    /// </summary>
    public class MqServerOptions : ChannelOptions
    {
        /// <summary>
        /// If true, server doesn't send client name to receivers.
        /// Enabling this feature gives you more privacy
        /// But peer to peer messaging is disabled.
        /// </summary>
        public bool HideClientNames { get; set; }
    }
}