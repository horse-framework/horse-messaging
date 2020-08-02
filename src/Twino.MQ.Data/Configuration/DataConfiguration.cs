using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.MQ.Data.Configuration
{
    /// <summary>
    /// Configuration for persistent queues
    /// </summary>
    internal class DataConfiguration
    {
        [JsonProperty("Channels")]
        [JsonPropertyName("Channels")]
        internal List<ChannelConfiguration> Channels { get; set; }

        public static DataConfiguration Empty()
        {
            DataConfiguration configuration = new DataConfiguration();
            configuration.Channels = new List<ChannelConfiguration>();
            return configuration;
        }
    }
}