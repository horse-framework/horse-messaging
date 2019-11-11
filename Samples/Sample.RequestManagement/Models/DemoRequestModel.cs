using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Twino.SocketModels;

namespace Sample.RequestManagement.Models
{
    public class DemoRequestModel : ISocketModel
    {
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public int Type { get; set; } = 101;
        
        [JsonProperty("text")]
        [JsonPropertyName("text")]
        public string Text { get; set; }
        
        [JsonProperty("number")]
        [JsonPropertyName("number")]
        public int Number { get; set; }
    }
}