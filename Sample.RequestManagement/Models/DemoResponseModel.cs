using Newtonsoft.Json;
using Twino.SocketModels;
using Twino.SocketModels.Serialization;

namespace Sample.RequestManagement.Models
{
    public class DemoResponseModel : IPerformanceCriticalModel
    {
        [JsonProperty("type")]
        public int Type { get; set; } = 102;
        
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("resultCode")]
        public int ResultCode { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; }
        
        public void Serialize(LightJsonWriter writer)
        {
            writer.Write("type", Type);
            writer.Write("id", Id);
            writer.Write("resultCode", ResultCode);
            writer.Write("message", Message);
        }

        public void Deserialize(LightJsonReader reader)
        {
            Type = reader.ReadInt32();
            Id = reader.ReadInt32();
            ResultCode = reader.ReadInt32("resultCode");
            Message = reader.ReadString();
        }
        
    }
}