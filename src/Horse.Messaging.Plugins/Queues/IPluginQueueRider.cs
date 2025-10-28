using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Horse.Messaging.Plugins.Queues;

/// <summary>
/// Server queue operator for plugins
/// </summary>
public interface IPluginQueueRider
{
    /// <summary>
    /// Gets all active queues
    /// </summary>
    IEnumerable<IPluginQueue> GetAll();

    /// <summary>
    /// Finds a queue by name.
    /// </summary>
    IPluginQueue Find(string name);

    /// <summary>
    /// Creates new queue with default options
    /// </summary>
    Task<IPluginQueue> Create(string name);

    /// <summary>
    /// Creates new queue with custom options.
    /// Options values will be cloned from server's default options.
    /// </summary>
    Task<IPluginQueue> Create(string name, Action<PluginQueueOptions> options);

    /// <summary>
    /// Removes a queue
    /// </summary>
    Task Remove(string name);

    /// <summary>
    /// Gets default server queue options.
    /// </summary>
    /// <returns></returns>
    PluginQueueOptions GetDefaultOptions();

    /// <summary>
    /// Sets default server queue options.
    /// </summary>
    void SetDefaultOptions(Action<PluginQueueOptions> options);
}