using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.MQ.Data.Configuration
{
    internal class ChannelConfiguration
    {
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonProperty("Configuration")]
        [JsonPropertyName("Configuration")]
        public ChannelOptionsConfiguration Configuration { get; set; }

        [JsonProperty("Queues")]
        [JsonPropertyName("Queues")]
        public List<QueueOptionsConfiguration> Queues { get; set; }
    }
}