using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins;

public class HorsePluginContext
{
    public HorsePluginEvent SourceEvent { get; }
    
    public HorsePlugin Plugin { get; }
    
    public HorseMessage Request { get; }

    public HorseMessage Response { get; set; }

    public HorsePluginContext(HorsePluginEvent sourceEvent, HorsePlugin plugin, HorseMessage request)
    {
        SourceEvent = sourceEvent;
        Plugin = plugin;
        Request = request;
    }
}