using System.Text.Json.Serialization;

namespace Twino.Protocols.TMQ.Models.Events
{
    /// <summary>
    /// Channel event info model
    /// </summary>
    public class ChannelEvent
    {
        /// <summary>
        /// Channel name
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Online and subscribed clients in the channel
        /// </summary>
        [JsonPropertyName("ActiveClients")]
        public int ActiveClients { get; set; }

        /// <summary>
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("ClientLimit")]
        public int ClientLimit { get; set; }

        /// <summary>
        /// If event is raised in different node instance, the name of the instance.
        /// If null, event is raised in same instance.
        /// </summary>
        [JsonPropertyName("Node")]
        public string Node { get; set; }
    }
}