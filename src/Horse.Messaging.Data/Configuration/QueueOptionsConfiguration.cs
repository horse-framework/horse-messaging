using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Horse.Messaging.Data.Configuration
{
    internal class QueueOptionsConfiguration
    {
        [JsonProperty("AcknowledgeTimeout")]
        [JsonPropertyName("AcknowledgeTimeout")]
        public int AcknowledgeTimeout { get; set; }

        [JsonProperty("MessageTimeout")]
        [JsonPropertyName("MessageTimeout")]
        public int MessageTimeout { get; set; }

        [JsonProperty("Acknowledge")]
        [JsonPropertyName("Acknowledge")]
        public string Acknowledge { get; set; }

        [JsonProperty("Type")]
        [JsonPropertyName("Type")]
        public string Type { get; set; }

        [JsonProperty("MessageLimit")]
        [JsonPropertyName("MessageLimit")]
        public int MessageLimit { get; set; }

        [JsonProperty("MessageSizeLimit")]
        [JsonPropertyName("MessageSizeLimit")]
        public ulong MessageSizeLimit { get; set; }

        [JsonProperty("ClientLimit")]
        [JsonPropertyName("ClientLimit")]
        public int ClientLimit { get; set; }

        [JsonProperty("AutoDestroy")]
        [JsonPropertyName("AutoDestroy")]
        public string AutoDestroy { get; set; }

        [JsonProperty("DelayBetweenMessages")]
        [JsonPropertyName("DelayBetweenMessages")]
        public int DelayBetweenMessages { get; set; }
        
        [JsonProperty("PutBackDelay")]
        [JsonPropertyName("PutBackDelay")]
        public int PutBackDelay { get; set; }
    }
}