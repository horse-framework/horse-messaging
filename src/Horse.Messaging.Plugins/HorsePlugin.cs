using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;

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

    private IHorsePluginHandler _requestHandler;
    private Timer[] _timers = [];

    internal void Set(IPluginRider rider)
    {
        Plugin = rider;
    }

    internal void SetName(string name)
    {
        Name = name;
    }

    public abstract Task Initialize();

    public virtual Task<bool> Remove()
    {
        foreach (Timer timer in _timers)
        {
            try
            {
                timer.Stop();
                timer.Dispose();
            }
            catch
            {
            }
        }

        _timers = [];
        Removed = true;
        Handlers.Clear();

        return Task.FromResult(true);
    }

    public void AddTimer(TimeSpan interval, IHorsePluginHandler handler)
    {
        string timerGuid = Guid.NewGuid().ToString();
        long milliseconds = Convert.ToInt64(interval.TotalMilliseconds);

        Handlers.Add($"@timer:{timerGuid}:{milliseconds}", handler);

        Timer timer = new Timer(interval.TotalMilliseconds);
        timer.Elapsed += (sender, args) => { handler.Execute(new HorsePluginContext(HorsePluginEvent.TimerElapse, this, Plugin, null)); };

        timer.AutoReset = true;
        timer.Start();

        var timers = _timers.ToList();
        timers.Add(timer);
        _timers = timers.ToArray();
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
        _requestHandler = handler;
    }

    internal IHorsePluginHandler GetRequestHandler() => _requestHandler;
}