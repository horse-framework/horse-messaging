using Newtonsoft.Json;
using Twino.SocketModels;

namespace Sample.RequestManagement.Models
{
    public class DemoRequestModel : ISocketModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 101;
        
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("number")]
        public int Number { get; set; }
    }
}