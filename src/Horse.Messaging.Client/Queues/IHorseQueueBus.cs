using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

/// <inheritdoc />
public interface IHorseQueueBus<TIdentifier> : IHorseQueueBus
{
}

/// <summary>
/// Bus interface for pushing messages to and pulling messages from Horse queues.
/// Provides overloads for raw binary (<see cref="MemoryStream"/> and <c>byte[]</c>) and model-based messages,
/// with optional support for commit waiting, custom headers, partition labels, and cancellation tokens.
/// </summary>
public interface IHorseQueueBus : IHorseConnection
{
    #region Push — raw MemoryStream

    /// <inheritdoc cref="Push(string, MemoryStream, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content)
        => Push(queue, content, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue without waiting for commit (fire-and-forget).
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, bool, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit)
        => Push(queue, content, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response.
    /// The commit timing depends on the queue's <c>CommitWhen</c> setting. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Push(queue, content, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => Push(queue, content, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with custom headers and a partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, string, bool, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit)
        => Push(queue, content, messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, string, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Push(queue, content, messageId, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id and custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => Push(queue, content, messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id, custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content as <see cref="MemoryStream"/>.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);

    #endregion

    #region Push — raw byte[]

    /// <inheritdoc cref="Push(string, byte[], CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data)
        => Push(queue, new MemoryStream(data), CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue without waiting for commit (fire-and-forget).
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), cancellationToken);

    /// <inheritdoc cref="Push(string, byte[], bool, CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit)
        => Push(queue, new MemoryStream(data), waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), waitForCommit, cancellationToken);

    /// <inheritdoc cref="Push(string, byte[], bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Push(queue, new MemoryStream(data), waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), waitForCommit, messageHeaders, cancellationToken);

    /// <inheritdoc cref="Push(string, byte[], bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => Push(queue, new MemoryStream(data), waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue with custom headers and a partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), waitForCommit, messageHeaders, partitionLabel, cancellationToken);

    /// <inheritdoc cref="Push(string, byte[], string, bool, CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit)
        => Push(queue, new MemoryStream(data), messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue with an explicit message id.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), messageId, waitForCommit, cancellationToken);

    /// <inheritdoc cref="Push(string, byte[], string, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
        => Push(queue, new MemoryStream(data), messageId, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue with an explicit message id and custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), messageId, waitForCommit, messageHeaders, cancellationToken);

    /// <inheritdoc cref="Push(string, byte[], string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => Push(queue, new MemoryStream(data), messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes raw byte array content into a queue with an explicit message id, custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw binary content as byte array. Internally converted to <see cref="MemoryStream"/>.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken)
        => Push(queue, new MemoryStream(data), messageId, waitForCommit, messageHeaders, partitionLabel, cancellationToken);

    #endregion

    #region Push — model

    /// <inheritdoc cref="Push{T}(T, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model) where T : class
        => Push(model, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the queue resolved from the model's <c>[QueueName]</c> attribute without waiting for commit (fire-and-forget).
    /// </summary>
    /// <typeparam name="T">Message model type. Must have a <c>[QueueName]</c> attribute.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, bool waitForCommit) where T : class
        => Push(model, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the queue resolved from the model's <c>[QueueName]</c> attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response.
    /// The commit timing depends on the queue's <c>CommitWhen</c> setting (e.g. <c>AfterReceived</c>, <c>AfterSent</c>, <c>AfterAcknowledge</c>).
    /// If <c>false</c>, the message is sent fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Push(model, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes a model with custom headers. Queue name resolved from the model's attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, bool, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, string partitionLabel) where T : class
        => Push(model, waitForCommit, null, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with a partition label, without custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, string partitionLabel, CancellationToken cancellationToken) where T : class
        => Push(model, waitForCommit, null, partitionLabel, cancellationToken);

    /// <inheritdoc cref="Push{T}(T, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(model, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with custom headers and a partition label.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit) where T : class
        => Push(queue, model, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name. Overrides the model's <c>[QueueName]</c> attribute.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Push(queue, model, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue with custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name. Overrides the model's <c>[QueueName]</c> attribute.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, bool, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, string partitionLabel) where T : class
        => Push(queue, model, waitForCommit, null, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue with a partition label, without custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, string partitionLabel, CancellationToken cancellationToken) where T : class
        => Push(queue, model, waitForCommit, null, partitionLabel, cancellationToken);

    /// <inheritdoc cref="Push{T}(string, T, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(queue, model, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue with custom headers and a partition label.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, string, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit) where T : class
        => Push(model, messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id. Queue name resolved from attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, string, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Push(model, messageId, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id and custom headers. Queue name resolved from attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(model, messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id, custom headers and partition label. Queue name resolved from attribute.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, string, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit) where T : class
        => Push(queue, model, messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, string, bool, IEnumerable{KeyValuePair{string,string}}, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => Push(queue, model, messageId, waitForCommit, messageHeaders, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue with custom headers.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(queue, model, messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue, with custom headers and partition label.
    /// This is the most complete model Push overload.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name. If <c>null</c>, resolved from the model's <c>[QueueName]</c> attribute.</param>
    /// <param name="model">The message model to serialize and send.</param>
    /// <param name="messageId">Explicit unique message identifier. If <c>null</c> or empty, auto-generated.</param>
    /// <param name="waitForCommit">If <c>true</c>, blocks until the server sends a commit response. If <c>false</c>, fire-and-forget.</param>
    /// <param name="messageHeaders">Additional key-value headers to attach to the message. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Messages with the same label are routed to the same partition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="HorseResult"/> indicating the operation result.</returns>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    #endregion

    #region PushBulk

    /// <inheritdoc cref="PushBulk{T}(string, List{T}, Action{HorseMessage, bool}, IEnumerable{KeyValuePair{string,string}}, string)"/>
    void PushBulk<T>(List<T> items) where T : class
        => PushBulk(null, items, null, null, null);

    /// <inheritdoc cref="PushBulk{T}(string, List{T}, Action{HorseMessage, bool}, IEnumerable{KeyValuePair{string,string}}, string)"/>
    void PushBulk<T>(List<T> items, Action<HorseMessage, bool> callback) where T : class
        => PushBulk(null, items, callback, null, null);

    /// <inheritdoc cref="PushBulk{T}(string, List{T}, Action{HorseMessage, bool}, IEnumerable{KeyValuePair{string,string}}, string)"/>
    void PushBulk<T>(List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
        => PushBulk(null, items, callback, messageHeaders, null);

    /// <inheritdoc cref="PushBulk{T}(string, List{T}, Action{HorseMessage, bool}, IEnumerable{KeyValuePair{string,string}}, string)"/>
    void PushBulk<T>(string queue, List<T> items) where T : class
        => PushBulk(queue, items, null, null, null);

    /// <inheritdoc cref="PushBulk{T}(string, List{T}, Action{HorseMessage, bool}, IEnumerable{KeyValuePair{string,string}}, string)"/>
    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback) where T : class
        => PushBulk(queue, items, callback, null, null);

    /// <summary>
    /// Pushes multiple model messages into a queue in a single batch.
    /// </summary>
    /// <typeparam name="T">Message model type.</typeparam>
    /// <param name="queue">Target queue name.</param>
    /// <param name="items">List of model instances to push. Each item is serialized and sent as a separate message.</param>
    /// <param name="callback">Optional callback invoked per message with the <see cref="HorseMessage"/> and a <c>bool</c> indicating commit success. Pass <c>null</c> if not needed.</param>
    /// <param name="messageHeaders">Additional key-value headers applied to all messages. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Pass <c>null</c> if not needed.</param>
    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class;

    /// <inheritdoc cref="PushBulk(string, List{MemoryStream}, bool, Action{HorseMessage, bool}, IEnumerable{KeyValuePair{string,string}}, string)"/>
    void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit)
        => PushBulk(queue, contents, waitForCommit, null, null, null);

    /// <summary>
    /// Pushes multiple raw binary messages into a queue in a single batch.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="contents">List of raw binary <see cref="MemoryStream"/> contents to push.</param>
    /// <param name="waitForCommit">If <c>true</c>, tracks commit responses from the server.</param>
    /// <param name="callback">Optional callback invoked per message with the <see cref="HorseMessage"/> and a <c>bool</c> indicating commit success.</param>
    /// <param name="messageHeaders">Additional key-value headers applied to all messages. Pass <c>null</c> if not needed.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing. Pass <c>null</c> if not needed.</param>
    void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit,
        Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel);

    #endregion

    #region Pull

    /// <inheritdoc cref="Pull(PullRequest, CancellationToken)"/>
    Task<PullContainer> Pull(PullRequest request)
        => Pull(request, CancellationToken.None);

    /// <summary>
    /// Sends a pull request to a Pull-type queue and returns the pulled messages.
    /// </summary>
    /// <param name="request">Pull request configuration specifying queue name, count, and other options.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="PullContainer"/> containing the pulled messages and status.</returns>
    Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="Pull(PullRequest, Func{int, HorseMessage, Task}, CancellationToken)"/>
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage)
        => Pull(request, actionForEachMessage, CancellationToken.None);

    /// <summary>
    /// Sends a pull request and invokes the action for each received message.
    /// </summary>
    /// <param name="request">Pull request configuration specifying queue name, count, and other options.</param>
    /// <param name="actionForEachMessage">Action invoked for each message. The <c>int</c> parameter is the message index (0-based), and the <see cref="HorseMessage"/> is the received message.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pending operation.</param>
    /// <returns>A <see cref="PullContainer"/> containing the pull operation results.</returns>
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken);

    #endregion
}
