using System.Text.Json.Serialization;

namespace Twino.Protocols.TMQ.Models.Events
{
    /// <summary>
    /// Node event info model
    /// </summary>
    public class NodeEvent
    {
        /// <summary>
        /// Node name
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; }
        
        /// <summary>
        /// Node host name
        /// </summary>
        [JsonPropertyName("Host")]
        public string Host { get; set; }
    }
}