namespace Twino.MQ.Options
{
    /// <summary>
    /// Distributed instance options
    /// </summary>
    public class InstanceOptions
    {
        /// <summary>
        /// Descriptor name for the instance
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Instance hostname
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Authentication token for the instance
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// If true, messages will will queued if instances are not connected, and they will be sent after short disconnections
        /// </summary>
        public bool KeepMessages { get; set; }

        /// <summary>
        /// How many milliseconds should wait to try reconnect
        /// </summary>
        public int ReconnectWait { get; set; }
    }
}