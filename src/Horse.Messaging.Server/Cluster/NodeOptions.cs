namespace Horse.Messaging.Server.Cluster
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
        /// Instance public hostname
        /// </summary>
        public string PublicHost { get; set; }

        /// <summary>
        /// Authentication token for the node
        /// </summary>
        public string Token { get; set; }
    }
}