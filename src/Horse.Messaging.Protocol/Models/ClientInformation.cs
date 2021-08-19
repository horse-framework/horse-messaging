using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Horse.Messaging.Protocol.Models
{
    /// <summary>
    /// Connected client information
    /// </summary>
    public class ClientInformation
    {
        /// <summary>
        /// Client's unique id
        /// If it's null or empty, server will create new unique id for the client.
        /// </summary>
        [JsonProperty("Id")]
        [JsonPropertyName("Id")]
        public string Id { get; set; }

        /// <summary>
        /// Client name
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Client type.
        /// If different type of clients join your server, you can categorize them with this type value
        /// </summary>
        [JsonProperty("Type")]
        [JsonPropertyName("Type")]
        public string Type { get; set; }

        /// <summary>
        /// Total online duration of client in milliseconds
        /// </summary>
        [JsonProperty("Online")]
        [JsonPropertyName("Online")]
        public long Online { get; set; }

        /// <summary>
        /// If true, client authenticated by server's IClientAuthenticator implementation
        /// </summary>
        [JsonProperty("IsAuthenticated")]
        [JsonPropertyName("IsAuthenticated")]
        public bool IsAuthenticated { get; set; }
    }
}