using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Horse.Messaging.Protocol.Models
{
    /// <summary>
    /// Instance information
    /// </summary>
    public class NodeInformation
    {
        /// <summary>
        /// Instance unique id
        /// </summary>
        [JsonProperty("Id")]
        [JsonPropertyName("Id")]
        public string Id { get; set; }

        /// <summary>
        /// Instance name
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Instance host name
        /// </summary>
        [JsonProperty("Host")]
        [JsonPropertyName("Host")]
        public string Host { get; set; }

        /// <summary>
        /// Node public host name
        /// </summary>
        [JsonProperty("PublicHost")]
        [JsonPropertyName("PublicHost")]
        public string PublicHost { get; set; }
        
        /// <summary>
        /// Node States: Main, Successor, Replica, Single
        /// </summary>
        [JsonProperty("State")]
        [JsonPropertyName("State")]
        public string State { get; set; }

        /// <summary>
        /// True, if connection is alive
        /// </summary>
        [JsonProperty("Connected")]
        [JsonPropertyName("Connected")]
        public bool IsConnected { get; set; }

        /// <summary>
        /// lifetime in milliseconds
        /// </summary>
        [JsonProperty("Lifetime")]
        [JsonPropertyName("Lifetime")]
        public long Lifetime { get; set; }
    }
}