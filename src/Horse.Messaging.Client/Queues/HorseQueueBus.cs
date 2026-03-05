using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

internal class HorseQueueBus<TIdentifier> : HorseQueueBus, IHorseQueueBus<TIdentifier>
{
    public HorseQueueBus(HorseClient client) : base(client) { }
}

/// <summary>
/// Default implementation of <see cref="IHorseQueueBus"/>.
/// Delegates every call to the underlying <see cref="HorseClient.Queue"/> operator.
/// </summary>
public class HorseQueueBus : IHorseQueueBus
{
    private readonly HorseClient _client;

    /// <summary>Creates a new queue bus wrapper for the given client.</summary>
    public HorseQueueBus(HorseClient client) => _client = client;

    /// <inheritdoc />
    public HorseClient GetClient() => _client;

    // ── Push — raw ──

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken)
        => _client.Queue.Push(queue, content, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken)
        => _client.Queue.Push(queue, content, waitForCommit, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken)
        => _client.Queue.Push(queue, content, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, CancellationToken cancellationToken)
        => _client.Queue.Push(queue, content, messageId, waitForCommit, null, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken)
        => _client.Queue.Push(queue, content, messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);

    // ── Push — model ──

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(model, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(model, waitForCommit, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(model, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(queue, model, waitForCommit, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(queue, model, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(model, messageId, waitForCommit, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(model, messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(queue, model, messageId, waitForCommit, null, cancellationToken);

    /// <inheritdoc />
    public Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class
        => _client.Queue.Push(queue, model, messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);

    // ── PushBulk ──

    /// <inheritdoc />
    public void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => _client.Queue.PushBulk(queue, items, callback, MergePartitionHeader(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit,
        Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => _client.Queue.PushBulk(queue, contents, waitForCommit, callback, MergePartitionHeader(partitionLabel, messageHeaders));

    // ── Pull ──

    /// <inheritdoc />
    public Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken)
        => _client.Queue.Pull(request, cancellationToken);

    /// <inheritdoc />
    public Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken)
        => _client.Queue.Pull(request, actionForEachMessage, cancellationToken);

    // ── Helpers ──

    private static IEnumerable<KeyValuePair<string, string>> MergePartitionHeader(
        string partitionLabel, IEnumerable<KeyValuePair<string, string>> extra)
    {
        if (string.IsNullOrEmpty(partitionLabel))
            return extra;

        var headers = new List<KeyValuePair<string, string>>
        {
            new(HorseHeaders.PARTITION_LABEL, partitionLabel)
        };

        if (extra != null)
            headers.AddRange(extra);

        return headers;
    }
}