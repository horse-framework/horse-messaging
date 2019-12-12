using Newtonsoft.Json;
using Twino.SerializableModel;

namespace Test.SocketModels.Models
{
    public class ResponseModel : ISerializableModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 101;

        [JsonProperty("delay")]
        public int Delay { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}