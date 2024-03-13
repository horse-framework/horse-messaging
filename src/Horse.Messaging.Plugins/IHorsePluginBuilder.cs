namespace Horse.Messaging.Plugins;

/// <summary>
/// Horse Plugin Builder.
/// In order to register your plugin into the Horse Server, your assembly must include a type implemented this interface
/// </summary>
public interface IHorsePluginBuilder
{
    public HorsePlugin Build();
}