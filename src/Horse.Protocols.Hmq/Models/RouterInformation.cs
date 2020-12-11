using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Horse.Protocols.Hmq.Models
{
    /// <summary>
    /// Router Information
    /// </summary>
    public class RouterInformation
    {
        /// <summary>
        /// Route name.
        /// Must be unique.
        /// Can't include " ", "*" or ";"
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// If true, messages are routed to bindings.
        /// If false, messages are not routed.
        /// </summary>
        [JsonProperty("IsEnabled")]
        [JsonPropertyName("IsEnabled")]
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Route method. Defines how messages will be routed.
        /// </summary>
        [JsonProperty("Method")]
        [JsonPropertyName("Method")]
        public RouteMethod Method { get; set; }
    }
}