using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.SerializableModel;

namespace Test.SocketModels.Models
{
    public class RequestModel : ISerializableModel
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