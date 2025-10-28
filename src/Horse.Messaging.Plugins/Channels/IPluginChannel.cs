using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Plugins.Channels;

/// <summary>
/// Represents a server channel for plugin system usage
/// </summary>
public interface IPluginChannel
{
    /// <summary>
    /// Channel name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Channel topic
    /// </summary>
    string Topic { get; }

    /// <summary>
    /// Channel status
    /// </summary>
    PluginChannelStatus Status { get; set; }

    /// <summary>
    /// Pushed a string message into the channel.
    /// </summary>
    PluginPushResult Push(string message);

    /// <summary>
    /// Pushed a horse message into the channel.
    /// </summary>
    PluginPushResult Push(HorseMessage message);

    /// <summary>
    /// Gets channel options
    /// </summary>
    PluginChannelOptions GetOptions();

    /// <summary>
    /// Sets channel options.
    /// </summary>
    void SetOptions(Action<PluginChannelOptions> options);

    /// <summary>
    /// Gets initial message of the channel.
    /// </summary>
    /// <returns></returns>
    Task<HorseMessage> GetInitialMessage();

    /// <summary>
    /// Gets channel information.
    /// </summary>
    /// <returns></returns>
    ChannelInformation GetInfo();

    /// <summary>
    /// Gets active subscribers of the channel.
    /// </summary>
    IEnumerable<IPluginMessagingClient> GetSubscribers();
}