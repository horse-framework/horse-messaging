using Horse.Messaging.Plugins.Clients;

namespace Horse.Messaging.Server.Plugins;

internal class PluginClientRider : IPluginClientRider
{
    private readonly HorseRider _rider;
    private readonly PluginRider _plugin;

    public PluginClientRider(HorseRider rider, PluginRider plugin)
    {
        _rider = rider;
        _plugin = plugin;
    }
}