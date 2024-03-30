namespace Horse.Messaging.Server.Plugins;

/// <summary>
/// Plugin configurator
/// </summary>
public class HorsePluginConfigurator
{
    /// <summary>
    /// Horse rider
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Plugin rider
    /// </summary>
    public PluginRider Plugin { get; }

    internal HorsePluginConfigurator(HorseRider rider)
    {
        Rider = rider;
        Plugin = rider.Plugin;
    }
}