using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Horse.Messaging.Data.Configuration
{
    /// <summary>
    /// Configuration for persistent queues
    /// </summary>
    internal class DataConfiguration
    {
        [JsonProperty("Queues")]
        [JsonPropertyName("Queues")]
        internal List<QueueConfiguration> Queues { get; set; }

        public static DataConfiguration Empty()
        {
            DataConfiguration configuration = new DataConfiguration();
            configuration.Queues = new List<QueueConfiguration>();
            return configuration;
        }
    }
}