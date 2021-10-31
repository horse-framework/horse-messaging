using System;

namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Node Info
    /// </summary>
    public class NodeInfo
    {
        /// <summary>
        /// Node Unique Id.
        /// That value changes each time node restarted.
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Node Name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Node Internal host name
        /// </summary>
        public string Host { get; set; }
        
        /// <summary>
        /// Node public host name for clients
        /// </summary>
        public string PublicHost { get; set; }

        /// <summary>
        /// Node start date
        /// </summary>
        public DateTime? StartDate { get; set; }
    }
}