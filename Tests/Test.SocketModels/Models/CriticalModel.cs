using Twino.SocketModels;
using Twino.SocketModels.Serialization;

namespace Test.SocketModels.Models
{
    public class CriticalModel : IPerformanceCriticalModel
    {
        public int Type { get; set; } = 302;

        public string Name { get; set; }
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