using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Plugins.Queues;

/// <summary>
/// Horse queue object for plugins
/// </summary>
public interface IPluginQueue
{
    /// <summary>
    /// Unique name (not case-sensetive)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Queue topic
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Queue status
    /// </summary>
    PluginQueueStatus Status { get; }

    /// <summary>
    /// Queue type
    /// </summary>
    PluginQueueType Type { get; }

    /// <summary>
    /// Queue manager name
    /// </summary>
    string ManagerName { get; }

    /// <summary>
    /// Gets queue information
    /// </summary>
    QueueInformation GetInfo();

    /// <summary>
    /// Sets queue status
    /// </summary>
    void SetStatus(PluginQueueStatus newStatus);

    /// <summary>
    /// Pushes a string message into the queue.
    /// </summary>
    Task<PluginPushResult> Push(string message, bool highPriority = false);

    /// <summary>
    /// Pushes a Horse message into the queue.
    /// </summary>
    Task<PluginPushResult> Push(HorseMessage message);

    /// <summary>
    /// Gets all consumers of the queue.
    /// </summary>
    IEnumerable<IPluginMessagingClient> GetConsumers();

    /// <summary>
    /// Gets queue options.
    /// </summary>
    PluginQueueOptions GetOptions();
    
    /// <summary>
    /// Sets queue options.
    /// </summary>
    void SetOptions(Action<PluginQueueOptions> options);
}