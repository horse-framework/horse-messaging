using System.Threading.Tasks;

namespace Horse.Messaging.Plugins;

/// <summary>
/// Event Handler implementation for Horse Plugins.
/// Each plugin can register one or multiple handlers for different events.
/// </summary>
public interface IHorsePluginHandler
{
    /// <summary>
    /// Content type of the event
    /// </summary>
    ushort ContentType { get; }

    /// <summary>
    /// This method is executed when the event is raised for the specified implementation
    /// </summary>
    public Task Execute(HorsePluginContext context);
}