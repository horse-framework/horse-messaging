using Newtonsoft.Json;
using System.Text.Json.Serialization;
using Twino.SerializableModel;

namespace Test.SocketModels.Models
{
    public class DefaultModel : ISerializableModel
    {
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public int Type { get; set; } = 301;

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonProperty("number")]
        [JsonPropertyName("number")]
        public int Number { get; set; }

    }
}