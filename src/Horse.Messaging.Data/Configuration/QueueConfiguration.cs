using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Data.Configuration
{
    internal class QueueConfiguration
    {
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonProperty("File")]
        [JsonPropertyName("File")]
        public string File { get; set; }

        [JsonProperty("DeleteWhen")]
        [JsonPropertyName("DeleteWhen")]
        public int DeleteWhen { get; set; }

        [JsonProperty("CommitWhen")]
        [JsonPropertyName("CommitWhen")]
        public int CommitWhen { get; set; }

        [JsonProperty("Configuration")]
        [JsonPropertyName("Configuration")]
        public QueueOptionsConfiguration Configuration { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public HorseQueue Queue { get; set; }
    }
}