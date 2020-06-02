using System.Text.Json.Serialization;

namespace Twino.Protocols.TMQ.Models.Events
{
    /// <summary>
    /// Client channel join/leave info model
    /// </summary>
    public class SubscriptionEvent
    {
        /// <summary>
        /// Channel name
        /// </summary>
        [JsonPropertyName("Channel")]
        public string Channel { get; set; }

        /// <summary>
        /// Client Id
        /// </summary>
        [JsonPropertyName("ClientId")]
        public string ClientId { get; set; }

        /// <summary>
        /// Client name
        /// </summary>
        [JsonPropertyName("ClientName")]
        public string ClientName { get; set; }

        /// <summary>
        /// Client Type
        /// </summary>
        [JsonPropertyName("ClientType")]
        public string ClientType { get; set; }

        /// <summary>
        /// If event is raised in different node instance, the name of the instance.
        /// If null, event is raised in same instance.
        /// </summary>
        [JsonPropertyName("Node")]
        public string Node { get; set; }
    }
}