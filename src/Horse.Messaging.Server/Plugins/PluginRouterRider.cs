using Horse.Messaging.Plugins.Routers;

namespace Horse.Messaging.Server.Plugins;

internal class PluginRouterRider : IPluginRouterRider
{
    private readonly HorseRider _rider;
    private readonly PluginRider _plugin;

    public PluginRouterRider(HorseRider rider, PluginRider plugin)
    {
        _rider = rider;
        _plugin = plugin;
    }
}