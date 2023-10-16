using System.Text.Json;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Routing;

internal class RouterClusterNotifier
{
    private readonly ClusterManager _cluster;
    private RouterRider _rider;

    public RouterClusterNotifier(RouterRider rider, ClusterManager cluster)
    {
        _cluster = cluster;
        _rider = rider;
    }

    internal void SendRouterCreated(Router router)
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        RouterConfiguration configuration = RouterConfiguration.Create(router);
        HorseMessage msg = new HorseMessage(MessageType.Cluster, router.Name, KnownContentTypes.CreateRouter);
        msg.SetStringContent(JsonSerializer.Serialize(configuration, SerializerFactory.Default()));
        _cluster.SendMessage(msg);
    }

    internal void SendRouterRemoved(Router router)
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, router.Name, KnownContentTypes.RemoveRouter);
        _cluster.SendMessage(msg);
    }

    internal void SendRouterAddBinding(Router router, Binding binding)
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        BindingConfiguration configuration = BindingConfiguration.Create(binding);
        HorseMessage msg = new HorseMessage(MessageType.Cluster, router.Name, KnownContentTypes.AddBinding);
        msg.SetStringContent(JsonSerializer.Serialize(configuration, SerializerFactory.Default()));
        _cluster.SendMessage(msg);
    }

    internal void SendRouterRemoveBinding(Router router, Binding binding)
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, router.Name, KnownContentTypes.RemoveBinding);
        msg.SetStringContent(binding.Name);
        _cluster.SendMessage(msg);
    }
}