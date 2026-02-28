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
/// Provides raw binary, typed model, bulk push, and pull patterns.
/// All push methods accept an optional <c>partitionLabel</c> for partition-aware routing.
/// </summary>
public interface IHorseQueueBus : IHorseConnection
{
    // ──────────────────────────────────────────────────────────────────
    // Push — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes raw binary content into a queue.
    /// When <paramref name="partitionLabel"/> is not null, the message is routed to the partition with that label.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id.
    /// When <paramref name="partitionLabel"/> is not null, the message is routed to the partition with that label.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId,
        bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null,
        CancellationToken cancellationToken = default);

    // ──────────────────────────────────────────────────────────────────
    // Push — model
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes a serialized model into a queue.
    /// The queue name is resolved from the <typeparamref name="T"/> attribute.
    /// When <paramref name="partitionLabel"/> is not null, the message is routed to the partition with that label.
    /// </summary>
    Task<HorseResult> Push<T>(T model, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Pushes a serialized model into the specified queue.
    /// When <paramref name="partitionLabel"/> is not null, the message is routed to the partition with that label.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Pushes a serialized model into a queue with an explicit message id.
    /// The queue name is resolved from the <typeparamref name="T"/> attribute.
    /// When <paramref name="partitionLabel"/> is not null, the message is routed to the partition with that label.
    /// </summary>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Pushes a serialized model into the specified queue with an explicit message id.
    /// When <paramref name="partitionLabel"/> is not null, the message is routed to the partition with that label.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null,
        CancellationToken cancellationToken = default) where T : class;

    // ──────────────────────────────────────────────────────────────────
    // PushBulk — model
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes multiple serialized models to a queue in a single batch.
    /// The <paramref name="callback"/> is invoked for each message with the server's commit result.
    /// When <paramref name="partitionLabel"/> is not null, every message in the batch is routed to the partition with that label.
    /// </summary>
    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null) where T : class;

    // ──────────────────────────────────────────────────────────────────
    // PushBulk — raw content
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes multiple raw binary contents to a queue in a single batch.
    /// The <paramref name="callback"/> is invoked for each message with the server's commit result.
    /// When <paramref name="partitionLabel"/> is not null, every message in the batch is routed to the partition with that label.
    /// </summary>
    void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit,
        Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null,
        string partitionLabel = null);

    // ──────────────────────────────────────────────────────────────────
    // Pull
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests messages from a pull-type queue.
    /// </summary>
    Task<PullContainer> Pull(PullRequest request,
        Func<int, HorseMessage, Task> actionForEachMessage = null,
        CancellationToken cancellationToken = default);
}
