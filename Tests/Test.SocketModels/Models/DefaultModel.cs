using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.SocketModels;

namespace Test.SocketModels.Models
{
    public class DefaultModel : ISocketModel
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