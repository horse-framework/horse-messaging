using Horse.Messaging.Plugins.Channels;
using Horse.Messaging.Plugins.Clients;
using Horse.Messaging.Plugins.Queues;
using Horse.Messaging.Plugins.Routers;

namespace Horse.Messaging.Plugins;

public interface IPluginRider
{
    /// <summary>
    /// Queue rider object manages all queues and their operations
    /// </summary>
    public IPluginQueueRider Queue { get; }

    /// <summary>
    /// Client rider object manages all clients and their operations
    /// </summary>
    public IPluginClientRider Client { get; }

    /// <summary>
    /// Router rider object manages all routers and their operations
    /// </summary>
    public IPluginRouterRider Router { get; }

    /// <summary>
    /// Channel rider object manages all channels and their operations
    /// </summary>
    public IPluginChannelRider Channel { get; }
}