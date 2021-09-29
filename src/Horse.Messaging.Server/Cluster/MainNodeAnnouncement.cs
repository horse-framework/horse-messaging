using System.Text.Json.Serialization;

namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// The model for announcement of the main node.
    /// When a node is choosen as main, It sends that message to all other nodes.
    /// </summary>
    public class MainNodeAnnouncement
    {
        /// <summary>
        /// Main node information
        /// </summary>
        [JsonPropertyName("Main")]
        public NodeInfo Main { get; set; }
        
        /// <summary>
        /// Successor node information
        /// </summary>
        [JsonPropertyName("Successor")]
        public NodeInfo Successor { get; set; }
    }
}