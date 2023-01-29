using Horse.Messaging.Server.Cluster;
using System.Text.Json.Serialization;

namespace Horse.Messaging.Server
{
    [JsonSerializable(typeof(NodeQueueInfo))]
    internal partial class NodeQueueInfoSerializerContext : JsonSerializerContext { }

    [JsonSerializable(typeof(MainNodeAnnouncement))]
    internal partial class MainNodeAnnouncementSerializerContext : JsonSerializerContext { }
}