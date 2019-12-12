using Newtonsoft.Json;
using Twino.SerializableModel;
using Twino.SerializableModel.Serialization;

namespace Test.SocketModels.Models
{
    public class CriticalModel : IPerformanceCriticalModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 302;

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("number")]
        public int Number { get; set; }

        public void Serialize(LightJsonWriter writer)
        {
            writer.Write("type", Type);
            writer.Write("name", Name);
            writer.Write("number", Number);
        }

        public void Deserialize(LightJsonReader reader)
        {
            Type = reader.ReadInt32();
            Name = reader.ReadString("name");
            Number = reader.ReadInt32();
        }
    }
}