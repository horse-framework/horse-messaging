using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.Client.TMQ.Annotations;

namespace Sample.Consumer
{
    [QueueName("model-a")]
    public class ModelA
    {
        [JsonProperty("no")]
        [JsonPropertyName("no")]
        public int No { get; set; }

        [JsonProperty("foo")]
        [JsonPropertyName("foo")]
        public string Foo { get; set; }
    }
}