using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
    Task<HorseResult> Create(string channel, CancellationToken cancellationToken);

    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Create(string channel, bool verifyResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Create(string channel, Action<ChannelOptions> options, bool verifyResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a channel
    /// </summary>
    Task<HorseResult> Delete(string channel, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a channel
    /// </summary>
    Task<HorseResult> Delete(string channel, bool verifyResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(object model, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(object model, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(string channel, object model, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(string channel, object model, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a string message to a channel
    /// </summary>
    Task<HorseResult> PublishString(string channel, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a string message to a channel
    /// </summary>
    Task<HorseResult> PublishString(string channel, string message, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a string message to a channel
    /// </summary>
    Task<HorseResult> PublishString(string channel, string message, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes binary data to a channel
    /// </summary>
    Task<HorseResult> PublishData(string channel, MemoryStream content, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes binary data to a channel
    /// </summary>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes binary data to a channel
    /// </summary>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
}