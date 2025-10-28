using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Horse.Messaging.Plugins.Channels;

/// <summary>
/// Server channel operator for plugins
/// </summary>
public interface IPluginChannelRider
{
    /// <summary>
    /// Gets all active channels
    /// </summary>
    IEnumerable<IPluginChannel> GetAll();

    /// <summary>
    /// Finds a channel by name.
    /// </summary>
    IPluginChannel Find(string name);

    /// <summary>
    /// Creates new channel with default options
    /// </summary>
    Task<IPluginChannel> Create(string name);

    /// <summary>
    /// Creates new channel with custom options.
    /// Options values will be cloned from server's default options.
    /// </summary>
    Task<IPluginChannel> Create(string name, Action<PluginChannelOptions> options);

    /// <summary>
    /// Removes a channel
    /// </summary>
    void Remove(string name);

    /// <summary>
    /// Gets default server channel options.
    /// </summary>
    PluginChannelOptions GetDefaultOptions();

    /// <summary>
    /// Sets default server channel options.
    /// </summary>
    void SetDefaultOptions(Action<PluginChannelOptions> options);
}