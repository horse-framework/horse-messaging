using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.MQ.Data.Configuration
{
    internal class QueueOptionsConfiguration
    {
        [JsonProperty("TagName")]
        [JsonPropertyName("TagName")]
        public string TagName { get; set; }

        [JsonProperty("SendOnlyFirstAcquirer")]
        [JsonPropertyName("SendOnlyFirstAcquirer")]
        public bool SendOnlyFirstAcquirer { get; set; }

        [JsonProperty("RequestAcknowledge")]
        [JsonPropertyName("RequestAcknowledge")]
        public bool RequestAcknowledge { get; set; }

        [JsonProperty("AcknowledgeTimeout")]
        [JsonPropertyName("AcknowledgeTimeout")]
        public int AcknowledgeTimeout { get; set; }

        [JsonProperty("MessageTimeout")]
        [JsonPropertyName("MessageTimeout")]
        public int MessageTimeout { get; set; }

        [JsonProperty("UseMessageId")]
        [JsonPropertyName("UseMessageId")]
        public bool UseMessageId { get; set; } = true;

        [JsonProperty("WaitForAcknowledge")]
        [JsonPropertyName("WaitForAcknowledge")]
        public bool WaitForAcknowledge { get; set; }

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
    }
}