using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Horse.Protocols.Hmq.Models
{
    /// <summary>
    /// Router binding information
    /// </summary>
    public class BindingInformation
    {
        /// <summary>
        /// Unique name of the binding
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Binding target name.
        /// For queue bindings, queue name.
        /// For direct bindings client id, type or name.
        /// </summary>
        [JsonProperty("Target")]
        [JsonPropertyName("Target")]
        public string Target { get; set; }

        /// <summary>
        /// Binding content type.
        /// Null, passes same content type from producer to receiver
        /// </summary>
        [JsonProperty("ContentType")]
        [JsonPropertyName("ContentType")]
        public ushort? ContentType { get; set; }

        /// <summary>
        /// Binding priority
        /// </summary>
        [JsonProperty("Priority")]
        [JsonPropertyName("Priority")]
        public int Priority { get; set; }

        /// <summary>
        /// Binding interaction type
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public BindingInteraction Interaction { get; set; }

        /// <summary>
        /// Binding type
        /// </summary>
        [JsonProperty("BindingType")]
        [JsonPropertyName("BindingType")]
        public BindingType BindingType { get; set; }

        /// <summary>
        /// Routing method in binding
        /// </summary>
        [JsonProperty("Method")]
        [JsonPropertyName("Method")]
        public RouteMethod Method { get; set; }
    }
}