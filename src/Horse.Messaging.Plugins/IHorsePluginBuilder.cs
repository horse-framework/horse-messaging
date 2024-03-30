namespace Horse.Messaging.Plugins;

/// <summary>
/// Horse Plugin Builder.
/// In order to register your plugin into the Horse Server, your assembly must include a type implemented this interface
/// </summary>
public interface IHorsePluginBuilder
{
    /// <summary>
    /// Returns name of the plugin
    /// </summary>
    /// <returns></returns>
    public string GetName();

    /// <summary>
    /// Build a new Horse Plugin
    /// </summary>
    /// <returns></returns>
    public HorsePlugin Build();
}