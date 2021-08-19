namespace Horse.Messaging.Server.Options
{
    /// <summary>
    /// Distributed node options
    /// </summary>
    public class NodeOptions
    {
        /// <summary>
        /// Descriptor name for the node
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Instance hostname
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Authentication token for the node
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// How many milliseconds should wait to try reconnect
        /// </summary>
        public int ReconnectWait { get; set; }
    }
}