namespace Twino.MQ.Options
{
    /// <summary>
    /// Client options
    /// </summary>
    public class ClientOptions
    {
        /// <summary>
        /// If true, bypass authentication and clients can create new queues
        /// </summary>
        public bool AllowCreateQueues { get; set; }

        /// <summary>
        /// If true, bypass authentication and clients can join all channels
        /// </summary>
        public bool AllowJoinChannels { get; set; }

        /// <summary>
        /// If true, clients can send direct messages between
        /// </summary>
        public bool AllowPeerMessaging { get; set; }

        /// <summary>
        /// If true, receivers won't see senders' client unique names
        /// </summary>
        public bool HideClientNames { get; set; }
    }
}