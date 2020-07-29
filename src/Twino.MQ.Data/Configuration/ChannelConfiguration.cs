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
        public List<QueueConfiguration> Queues { get; set; }
        
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Channel Channel { get; set; }
        
        public static ChannelConfiguration Empty()
        {
            ChannelConfiguration configuration = new ChannelConfiguration();
            
            configuration.Configuration = new ChannelOptionsConfiguration();
            configuration.Queues = new List<QueueConfiguration>();
            
            return configuration;
        }
    }
}