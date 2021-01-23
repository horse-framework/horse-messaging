using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Horse.Protocols.Hmq.Models
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
        /// If true, connection is outgoing.
        /// This server is sending data to remote.
        /// If false, connection is incoming.
        /// Remote server is sending data tis server.
        /// </summary>
        [JsonProperty("Slave")]
        [JsonPropertyName("Slave")]
        public bool IsSlave { get; set; }

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