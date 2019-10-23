using Newtonsoft.Json;
using Twino.SocketModels;

namespace Test.SocketModels.Models
{
    public class DefaultModel : ISocketModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 301;

        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("number")]
        public int Number { get; set; }

    }
}