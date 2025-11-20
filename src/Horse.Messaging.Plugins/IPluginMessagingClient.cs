using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Plugins;

/// <summary>
/// Represents each client object which is connected to the server
/// </summary>
public interface IPluginMessagingClient
{
    /// <summary>
    /// Unique id of the client.
    /// </summary>
    string UniqueId { get; }

    /// <summary>
    /// Name of the client.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Type of the client.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Client connection data.
    /// Includes methods and connection parameters.
    /// </summary>
    ConnectionData Data { get; }

    /// <summary>
    /// True if the client is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Client's connection date
    /// </summary>
    DateTime ConnectedDate { get; }

    /// <summary>
    /// Client's remove ip address
    /// </summary>
    string RemoteHost { get; }

    /// <summary>
    /// Gets queues that the client is subscribed to.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetQueues();

    /// <summary>
    /// Gets channels that the client is subscribed to.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetChannels();

    /// <summary>
    /// Sends a message to the client.
    /// </summary>
    Task<bool> SendMessage(HorseMessage message);
}