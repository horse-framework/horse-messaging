using System.Text.Json.Serialization;

namespace Horse.Messaging.Server.Cluster
{
    public class MainNodeAnnouncement
    {
        [JsonPropertyName("Main")]
        public NodeInfo Main { get; set; }
        
        [JsonPropertyName("Successor")]
        public NodeInfo Successor { get; set; }
    }
}