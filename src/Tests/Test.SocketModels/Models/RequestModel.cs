using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.SocketModels;

namespace Test.SocketModels.Models
{
    public class RequestModel : ISocketModel
    {
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public int Type { get; set; } = 100;
        
        [JsonProperty("delay")]
        [JsonPropertyName("delay")]
        public int Delay { get; set; }
        
        [JsonProperty("value")]
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }
}