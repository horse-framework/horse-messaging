using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.MQ.Data.Configuration
{
    internal class QueueConfiguration
    {
        [JsonProperty("Channel")]
        [JsonPropertyName("Channel")]
        public string Channel { get; set; }

        [JsonProperty("QueueId")]
        [JsonPropertyName("QueueId")]
        public ushort QueueId { get; set; }

        [JsonProperty("Configuration")]
        [JsonPropertyName("Configuration")]
        public QueueOptionsConfiguration Configuration { get; set; }
    }
}