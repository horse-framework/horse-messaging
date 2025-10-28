using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;

[assembly: InternalsVisibleTo("Horse.Messaging.Server")]

namespace Horse.Messaging.Plugins;

/// <summary>
/// Horse plugin base class
/// </summary>
public abstract class HorsePlugin
{
    /// <summary>
    /// Plugin name
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// Plugin description
    /// </summary>
    public string Description { get; protected set; }

    /// <summary>
    /// If the plugin is initialized
    /// </summary>
    public bool Initialized { get; internal set; }

    /// <summary>
    /// If the plugin is removed
    /// </summary>
    public bool Removed { get; internal set; }

    /// <summary>
    /// Rider object for the plugin. Rider object is used for server side operations.
    /// </summary>
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

    /// <summary>
    /// Initializes the plugin.
    /// </summary>
    public abstract Task Initialize();

    /// <summary>
    /// Removes the plugin from the server
    /// </summary>
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

    /// <summary>
    /// Create a timer and add it to the plugin.
    /// It executes the handler every interval.
    /// </summary>
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

    /// <summary>
    /// Subscribe to specified channel messages.
    /// Messages will be delivered to the handler.
    /// But the plugin will not show up in the client list of the channel.
    /// </summary>
    public void AddChannelPublish(string channelName, IHorsePluginHandler handler)
    {
        Handlers.Add($"@channel:{channelName}", handler);
    }

    /// <summary>
    /// Subscribes to specified queue messages.
    /// Messages will be delivered to the handler.
    /// But the plugin will not show up in the client list of the queue.
    /// This handler does not consume any message, it just listens to the queue.
    /// </summary>
    public void AddQueuePush(string queueName, IHorsePluginHandler handler)
    {
        Handlers.Add($"@queue:{queueName}", handler);
    }

    /// <summary>
    /// Subscribes to specified router messages.
    /// </summary>
    public void AddRouterPublish(string routerName, IHorsePluginHandler handler)
    {
        Handlers.Add($"@router:{routerName}", handler);
    }

    /// <summary>
    /// Request handler for the plugin messages.
    /// If you want to send a request to the plugin, you should use this handler.
    /// </summary>
    public void AddRequestHandler(IHorsePluginHandler handler, bool isDefault = true)
    {
        Handlers.Add($"@plugin:" + handler.ContentType, handler);

        if (isDefault)
            _requestHandler = handler;
    }

    internal IHorsePluginHandler GetDefaultRequestHandler() => _requestHandler;

    internal IHorsePluginHandler GetRequestHandler(ushort contentType)
    {
        Handlers.TryGetValue("@plugin:" + contentType, out var value);
        return value;
    }
}