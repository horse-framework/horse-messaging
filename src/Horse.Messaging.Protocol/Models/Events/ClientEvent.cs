using System.Text.Json.Serialization;

namespace Horse.Messaging.Protocol.Models.Events
{
    /// <summary>
    /// Client event info model
    /// </summary>
    public class ClientEvent
    {
        /// <summary>
        /// Client Id
        /// </summary>
        [JsonPropertyName("Id")]
        public string Id { get; set; }
        
        /// <summary>
        /// Client name
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Client Type
        /// </summary>
        [JsonPropertyName("Type")]
        public string Type { get; set; }
        
        /// <summary>
        /// If event is raised in different node instance, the name of the instance.
        /// If null, event is raised in same instance.
        /// </summary>
        [JsonPropertyName("Node")]
        public string Node { get; set; }
    }
}