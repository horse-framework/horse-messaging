using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Plugins.Cache;
using Horse.Messaging.Plugins.Channels;
using Horse.Messaging.Plugins.Queues;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins;

/// <summary>
/// Horse plugin rider interface
/// </summary>
public interface IPluginRider
{
    /// <summary>
    /// Cache operation manager
    /// </summary>
    IPluginCacheRider Cache { get; }

    /// <summary>
    /// Queue operation manager
    /// </summary>
    IPluginQueueRider Queue { get; }

    /// <summary>
    /// Channel operation manager
    /// </summary>
    IPluginChannelRider Channel { get; }

    /// <summary>
    /// Returns server ports for plain horse protocol
    /// Server port could be used if plugin needs some client operations, it can connect to the server itself with using a horse client
    /// </summary>
    IEnumerable<int> GetServerPorts();

    /// <summary>
    /// Returns server ports for secure horse protocol.
    /// Server port could be used if plugin needs some client operations, it can connect to the server itself with using a horse client
    /// </summary>
    IEnumerable<int> GetSecureServerPorts();

    /// <summary>
    /// Sends a message from plugin to server or client 
    /// </summary>
    public Task<bool> SendMessage(HorseMessage message);
}