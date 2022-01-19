using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Horse.Messaging.Protocol.Models
{
    /// <summary>
    /// Cache key information
    /// </summary>
    public class CacheInformation
    {
        /// <summary>
        /// Cache key
        /// </summary>
        [JsonProperty("Key")]
        [JsonPropertyName("Key")]
        public string Key { get; set; }
        
        /// <summary>
        /// Key expiration in unix milliseconds
        /// </summary>
        [JsonProperty("Expiration")]
        [JsonPropertyName("Expiration")]
        public long Expiration { get; set; }
    }
}