namespace Horse.Messaging.Plugins;

/// <summary>
/// Horse Plugin Source Events
/// </summary>
public enum HorsePluginEvent
{
    /// <summary>
    /// Triggered when a client sends message with target @plugin:PluginName
    /// </summary>
    PluginMessage = 0,

    /// <summary>
    /// Triggered when a client publish message to the registered channel
    /// </summary>
    ChannelPublish = 1,

    /// <summary>
    /// Triggered when a client push message to the registered queue
    /// </summary>
    QueuePush = 2,

    /// <summary>
    /// Triggered when a client publish message to the registered router
    /// </summary>
    RouterPublish = 3,

    /// <summary>
    /// Triggered periodically in specified interval
    /// </summary>
    TimerElapse = 4,
}