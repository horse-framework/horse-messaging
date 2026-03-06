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
    /// <inheritdoc cref="Create(string, CancellationToken)"/>
    Task<HorseResult> Create(string channel)
        => Create(channel, CancellationToken.None);

    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Create(string channel, CancellationToken cancellationToken);

    /// <inheritdoc cref="Create(string, bool, CancellationToken)"/>
    Task<HorseResult> Create(string channel, bool verifyResponse)
        => Create(channel, verifyResponse, CancellationToken.None);

    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Create(string channel, bool verifyResponse, CancellationToken cancellationToken);

    /// <inheritdoc cref="Create(string, Action{ChannelOptions}, bool, CancellationToken)"/>
    Task<HorseResult> Create(string channel, Action<ChannelOptions> options, bool verifyResponse)
        => Create(channel, options, verifyResponse, CancellationToken.None);

    /// <summary>
    /// Creates new channel
    /// </summary>
    Task<HorseResult> Create(string channel, Action<ChannelOptions> options, bool verifyResponse, CancellationToken cancellationToken);

    /// <inheritdoc cref="Delete(string, CancellationToken)"/>
    Task<HorseResult> Delete(string channel)
        => Delete(channel, CancellationToken.None);

    /// <summary>
    /// Deletes a channel
    /// </summary>
    Task<HorseResult> Delete(string channel, CancellationToken cancellationToken);

    /// <inheritdoc cref="Delete(string, bool, CancellationToken)"/>
    Task<HorseResult> Delete(string channel, bool verifyResponse)
        => Delete(channel, verifyResponse, CancellationToken.None);

    /// <summary>
    /// Deletes a channel
    /// </summary>
    Task<HorseResult> Delete(string channel, bool verifyResponse, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(object, CancellationToken)"/>
    Task<HorseResult> Publish(object model)
        => Publish(model, CancellationToken.None);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(object model, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(object, bool, CancellationToken)"/>
    Task<HorseResult> Publish(object model, bool waitForAcknowledge)
        => Publish(model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(object model, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(string, object, bool, CancellationToken)"/>
    Task<HorseResult> Publish(string channel, object model, bool waitForAcknowledge)
        => Publish(channel, model, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(string channel, object model, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="Publish(string, object, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Publish(string channel, object model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Publish(channel, model, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a message to a channel
    /// </summary>
    Task<HorseResult> Publish(string channel, object model, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishString(string, string, CancellationToken)"/>
    Task<HorseResult> PublishString(string channel, string message)
        => PublishString(channel, message, CancellationToken.None);

    /// <summary>
    /// Publishes a string message to a channel
    /// </summary>
    Task<HorseResult> PublishString(string channel, string message, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishString(string, string, bool, CancellationToken)"/>
    Task<HorseResult> PublishString(string channel, string message, bool waitForAcknowledge)
        => PublishString(channel, message, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes a string message to a channel
    /// </summary>
    Task<HorseResult> PublishString(string channel, string message, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishString(string, string, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> PublishString(string channel, string message, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishString(channel, message, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes a string message to a channel
    /// </summary>
    Task<HorseResult> PublishString(string channel, string message, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishData(string, MemoryStream, CancellationToken)"/>
    Task<HorseResult> PublishData(string channel, MemoryStream content)
        => PublishData(channel, content, CancellationToken.None);

    /// <summary>
    /// Publishes binary data to a channel
    /// </summary>
    Task<HorseResult> PublishData(string channel, MemoryStream content, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishData(string, MemoryStream, bool, CancellationToken)"/>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge)
        => PublishData(channel, content, waitForAcknowledge, CancellationToken.None);

    /// <summary>
    /// Publishes binary data to a channel
    /// </summary>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge, CancellationToken cancellationToken);

    /// <inheritdoc cref="PublishData(string, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => PublishData(channel, content, waitForAcknowledge, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Publishes binary data to a channel
    /// </summary>
    Task<HorseResult> PublishData(string channel, MemoryStream content, bool waitForAcknowledge,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);
}