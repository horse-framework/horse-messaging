using System.ComponentModel;

namespace Horse.Messaging.Plugins.Queues;

/// <summary>
/// Queue type
/// </summary>
public enum PluginQueueType
{
    /// <summary>
    /// Queue messaging is in running state.
    /// Producers push the message into the queue and consumer receive when message is pushed
    /// </summary>
    [Description("push")]
    Push,

    /// <summary>
    /// Load balancing status. Queue messaging is in running state.
    /// Producers push the message into the queue and consumer receive when message is pushed.
    /// If there are no available consumers, message will be kept in queue like push status.
    /// </summary>
    [Description("round-robin")]
    RoundRobin,

    /// <summary>
    /// Queue messaging is in running state.
    /// Producers push message into queue, consumers receive the messages when they requested.
    /// Each message is sent only one-receiver at same time.
    /// Request operation removes the message from the queue.
    /// </summary>
    [Description("pull")]
    Pull
}