using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

[assembly: InternalsVisibleTo("Horse.Messaging.Server")]

namespace Horse.Messaging.Plugins;

public abstract class HorsePlugin
{
    public string Name { get; protected set; }
    public string Description { get; protected set; }

    public bool Initialized { get; internal set; }
    public bool Removed { get; internal set; }

    protected IPluginRider Plugin { get; private set; }

    internal SortedDictionary<string, IHorsePluginHandler> Handlers { get; } = new SortedDictionary<string, IHorsePluginHandler>(StringComparer.InvariantCultureIgnoreCase);

    internal void Set(IPluginRider rider)
    {
        Plugin = rider;
    }

    public abstract Task Initialize();

    public abstract Task<bool> Remove();

    public void AddTimer(TimeSpan interval, IHorsePluginHandler handler)
    {
        string timerGuid = Guid.NewGuid().ToString();
        long milliseconds = Convert.ToInt64(interval.TotalMilliseconds);
        Handlers.Add($"@timer:{timerGuid}:{milliseconds}", handler);
    }

    public void AddChannelPublish(string channelName, IHorsePluginHandler handler)
    {
        Handlers.Add($"@channel:{channelName}", handler);
    }

    public void AddQueuePush(string queueName, IHorsePluginHandler handler)
    {
        Handlers.Add($"@queue:{queueName}", handler);
    }

    public void AddRouterPublish(string routerName, IHorsePluginHandler handler)
    {
        Handlers.Add($"@router:{routerName}", handler);
    }

    public void AddRequestHandler(IHorsePluginHandler handler)
    {
        Handlers.Add($"@plugin", handler);
    }

    public async Task SendMessage(HorseMessage message)
    {
        throw new NotImplementedException();
    }
}