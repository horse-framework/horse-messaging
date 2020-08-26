using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.MQ.Queues;

namespace Twino.MQ.Data.Configuration
{
    internal class QueueConfiguration
    {
        [JsonProperty("QueueId")]
        [JsonPropertyName("QueueId")]
        public ushort QueueId { get; set; }

        [JsonProperty("Channel")]
        [JsonPropertyName("Channel")]
        public string Channel { get; set; }

        [JsonProperty("File")]
        [JsonPropertyName("File")]
        public string File { get; set; }

        [JsonProperty("DeleteWhen")]
        [JsonPropertyName("DeleteWhen")]
        public int DeleteWhen { get; set; }

        [JsonProperty("ProducerAck")]
        [JsonPropertyName("ProducerAck")]
        public int ProducerAck { get; set; }

        [JsonProperty("Configuration")]
        [JsonPropertyName("Configuration")]
        public QueueOptionsConfiguration Configuration { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public TwinoQueue Queue { get; set; }
    }
}