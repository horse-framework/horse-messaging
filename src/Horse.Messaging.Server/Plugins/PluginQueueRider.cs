using Horse.Messaging.Plugins.Queues;

namespace Horse.Messaging.Server.Plugins;

internal class PluginQueueRider : IPluginQueueRider
{
    private readonly HorseRider _rider;
    private readonly PluginRider _plugin;

    public PluginQueueRider(HorseRider rider, PluginRider plugin)
    {
        _rider = rider;
        _plugin = plugin;
    }
}