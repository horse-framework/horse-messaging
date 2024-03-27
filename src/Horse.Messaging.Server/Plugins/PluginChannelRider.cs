using Horse.Messaging.Plugins.Channels;

namespace Horse.Messaging.Server.Plugins;

internal class PluginChannelRider : IPluginChannelRider
{
    private readonly HorseRider _rider;
    private readonly PluginRider _plugin;

    public PluginChannelRider(HorseRider rider, PluginRider plugin)
    {
        _rider = rider;
        _plugin = plugin;
    }
}