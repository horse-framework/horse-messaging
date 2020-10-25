using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.MQ.Data.Configuration
{
    internal class QueueOptionsConfiguration
    {
        [JsonProperty("AcknowledgeTimeout")]
        [JsonPropertyName("AcknowledgeTimeout")]
        public int AcknowledgeTimeout { get; set; }

        [JsonProperty("MessageTimeout")]
        [JsonPropertyName("MessageTimeout")]
        public int MessageTimeout { get; set; }

        [JsonProperty("UseMessageId")]
        [JsonPropertyName("UseMessageId")]
        public bool UseMessageId { get; set; } = true;

        [JsonProperty("Acknowledge")]
        [JsonPropertyName("Acknowledge")]
        public string Acknowledge { get; set; }

        [JsonProperty("HideClientNames")]
        [JsonPropertyName("HideClientNames")]
        public bool HideClientNames { get; set; }

        [JsonProperty("Status")]
        [JsonPropertyName("Status")]
        public string Status { get; set; }

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
    }
}