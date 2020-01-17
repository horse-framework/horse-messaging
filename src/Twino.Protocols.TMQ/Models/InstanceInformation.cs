using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.Protocols.TMQ.Models
{
    /// <summary>
    /// Instance information
    /// </summary>
    public class InstanceInformation
    {
        /// <summary>
        /// If true, connection is outgoing.
        /// This server is sending data to remote.
        /// If false, connection is incoming.
        /// Remote server is sending data tis server.
        /// </summary>
        [JsonProperty("isSlave")]
        [JsonPropertyName("isSlave")]
        public bool IsSlave { get; set; }

        /// <summary>
        /// Instance host name
        /// </summary>
        [JsonProperty("host")]
        [JsonPropertyName("host")]
        public string Host { get; set; }

        /// <summary>
        /// Instance unique id
        /// </summary>
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// Instance name
        /// </summary>
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Instance type
        /// </summary>
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Connected date in unix milliseconds
        /// </summary>
        [JsonProperty("connectedDate")]
        [JsonPropertyName("connectedDate")]
        public long ConnectedDate { get; set; }
    }
}