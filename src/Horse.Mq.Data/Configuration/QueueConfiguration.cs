using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Horse.Mq.Queues;

namespace Horse.Mq.Data.Configuration
{
    internal class QueueConfiguration
    {
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonProperty("File")]
        [JsonPropertyName("File")]
        public string File { get; set; }

        [JsonProperty("DeliveryHandler")]
        [JsonPropertyName("DeliveryHandler")]
        public string DeliveryHandler { get; set; }

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
        public HorseQueue Queue { get; set; }
    }
}