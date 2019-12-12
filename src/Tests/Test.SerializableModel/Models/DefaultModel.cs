using Newtonsoft.Json;
using Twino.SerializableModel;

namespace Test.SocketModels.Models
{
    public class DefaultModel : ISerializableModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 301;

        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("number")]
        public int Number { get; set; }

    }
}