using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

/// <inheritdoc />
public interface IHorseChannelBus<TIdentifier> : IHorseChannelBus
{
}

/// <summary>
/// Messager implementation for Horse Channels
/// </summary>
public interface IHorseChannelBus
{
    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Create(string channel, Action<ChannelOptions> options = null, bool verifyResponse = false);

    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Delete(string channel, bool verifyResponse = false);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    /// <param name="model">Model</param>
    /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
    /// <param name="messageHeaders">Additional message headers</param>
    /// <returns></returns>
    Task<HorseResult> Publish(object model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    /// <param name="channel">Channel name</param>
    /// <param name="model">Model</param>
    /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
    /// <param name="messageHeaders">Additional message headers</param>
    /// <returns></returns>
    Task<HorseResult> Publish(string channel, object model, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    /// <param name="channel">Channel name</param>
    /// <param name="message">String message</param>
    /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
    /// <param name="messageHeaders">Additional message headers</param>
    /// <returns></returns>
    Task<HorseResult> PublishString(string channel, string message, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    /// <param name="channel">Channel name</param>
    /// <param name="content">Binary message</param>
    /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
    /// <param name="messageHeaders">Additional message headers</param>
    /// <returns></returns>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);
}