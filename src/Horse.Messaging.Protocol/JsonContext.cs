using Horse.Messaging.Protocol.Models;
using System.Text.Json.Serialization;

namespace Horse.Messaging.Protocol
{
    [JsonSerializable(typeof(BindingInformation))]
    internal partial class BindingInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(CacheInformation))]
    internal partial class CacheInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(ChannelInformation))]
    internal partial class ChannelInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(ClientInformation))]
    internal partial class ClientInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(NodeInformation))]
    internal partial class NodeInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(QueueInformation))]
    internal partial class QueueInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(RouterInformation))]
    internal partial class RouterInformationSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(HorseMessage))]
    internal partial class HorseMessageSerializerContext : JsonSerializerContext { }
}