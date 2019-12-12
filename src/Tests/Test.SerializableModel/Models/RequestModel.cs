using Newtonsoft.Json;
using Twino.SerializableModel;

namespace Test.SocketModels.Models
{
    public class RequestModel : ISerializableModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 100;
        
        [JsonProperty("delay")]
        public int Delay { get; set; }
        
        [JsonProperty("value")]
        public string Value { get; set; }
    }
}