using System.Text.Json;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Channels;

internal class ChannelClusterNotifier
{
    private readonly ChannelRider _rider;
    private readonly ClusterManager _cluster;

    public ChannelClusterNotifier(ChannelRider rider, ClusterManager cluster)
    {
        _rider = rider;
        _cluster = cluster;
    }

    internal void SendChannelCreated(HorseChannel channel)
    {
        HorseMessage msg = new HorseMessage(MessageType.Cluster, channel.Name, KnownContentTypes.ChannelCreate);
        msg.SetStringContent(JsonSerializer.Serialize(channel.Options, SerializerFactory.Default()));
        _cluster.SendMessage(msg);
    }

    internal void SendChannelUpdated(HorseChannel channel)
    {
        HorseMessage msg = new HorseMessage(MessageType.Cluster, channel.Name, KnownContentTypes.ChannelUpdate);
        msg.SetStringContent(JsonSerializer.Serialize(channel.Options, SerializerFactory.Default()));
        _cluster.SendMessage(msg);
    }

    internal void SendChannelRemoved(HorseChannel channel)
    {
        HorseMessage msg = new HorseMessage(MessageType.Cluster, channel.Name, KnownContentTypes.ChannelRemove);
        _cluster.SendMessage(msg);
    }
}