using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.MQ.Data.Configuration
{
    internal class ChannelOptionsConfiguration : QueueOptionsConfiguration
    {
        [JsonProperty("AllowMultipleQueues")]
        [JsonPropertyName("AllowMultipleQueues")]
        public bool AllowMultipleQueues { get; set; } = true;

        [JsonProperty("AllowedQueues")]
        [JsonPropertyName("AllowedQueues")]
        public ushort[] AllowedQueues { get; set; }

        [JsonProperty("ClientLimit")]
        [JsonPropertyName("ClientLimit")]
        public int ClientLimit { get; set; }

        [JsonProperty("QueueLimit")]
        [JsonPropertyName("QueueLimit")]
        public int QueueLimit { get; set; }

        [JsonProperty("DestroyWhenEmpty")]
        [JsonPropertyName("DestroyWhenEmpty")]
        public bool DestroyWhenEmpty { get; set; }
    }
}