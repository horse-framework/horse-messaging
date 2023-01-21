using Horse.Messaging.Protocol.Models;
using System.Text.Json.Serialization;

namespace Horse.Messaging.Protocol
{
    [JsonSerializable(typeof(BindingInformation))]
    [JsonSerializable(typeof(CacheInformation))]
    [JsonSerializable(typeof(ChannelInformation))]
    [JsonSerializable(typeof(ClientInformation))]
    [JsonSerializable(typeof(NodeInformation))]
    [JsonSerializable(typeof(QueueInformation))]
    [JsonSerializable(typeof(RouterInformation))]
    public partial class HorseJsonSerializerContext : JsonSerializerContext { }
}