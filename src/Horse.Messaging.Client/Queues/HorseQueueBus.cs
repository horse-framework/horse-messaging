using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

internal class HorseQueueBus<TIdentifier> : HorseQueueBus, IHorseQueueBus<TIdentifier>
{
    public HorseQueueBus(HorseClient client) : base(client) { }
}

/// <summary>
/// Implementation for queue messages and requests
/// </summary>
public class HorseQueueBus : IHorseQueueBus
{
    private readonly HorseClient _client;

    public HorseQueueBus(HorseClient client) => _client = client;

    /// <inheritdoc />
    public HorseClient GetClient() => _client;

    // ──────────────────────────────────────────────────────────────────
    // Push — standard
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, waitForCommit, messageHeaders);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, string content, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, waitForCommit, messageHeaders);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, MemoryStream content, string messageId,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, messageId, waitForCommit, messageHeaders);

    /// <inheritdoc />
    public Task<HorseResult> Push(string queue, string content, string messageId,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, messageId, waitForCommit, messageHeaders);

    // ──────────────────────────────────────────────────────────────────
    // PushToPartition
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> PushToPartition(string queue, string partitionLabel, MemoryStream content,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public Task<HorseResult> PushToPartition(string queue, string partitionLabel, string content,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public Task<HorseResult> PushToPartition(string queue, string partitionLabel, MemoryStream content,
        string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, messageId, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public Task<HorseResult> PushToPartition(string queue, string partitionLabel, string content,
        string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.Push(queue, content, messageId, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    // ──────────────────────────────────────────────────────────────────
    // PushJson — standard
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> PushJson(object jsonObject, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(jsonObject, waitForCommit, messageHeaders);

    /// <inheritdoc />
    public Task<HorseResult> PushJson(string queue, object jsonObject, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(queue, jsonObject, waitForCommit, messageHeaders);

    /// <inheritdoc />
    public Task<HorseResult> PushJson(object jsonObject, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(jsonObject, messageId, waitForCommit, messageHeaders);

    /// <inheritdoc />
    public Task<HorseResult> PushJson(string queue, object jsonObject, string messageId,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(queue, jsonObject, messageId, waitForCommit, messageHeaders);

    // ──────────────────────────────────────────────────────────────────
    // PushJsonToPartition
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<HorseResult> PushJsonToPartition(string partitionLabel, object jsonObject,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(jsonObject, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public Task<HorseResult> PushJsonToPartition(string queue, string partitionLabel, object jsonObject,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(queue, jsonObject, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public Task<HorseResult> PushJsonToPartition(string partitionLabel, object jsonObject, string messageId,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(jsonObject, messageId, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    /// <inheritdoc />
    public Task<HorseResult> PushJsonToPartition(string queue, string partitionLabel, object jsonObject,
        string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        => _client.Queue.PushJson(queue, jsonObject, messageId, waitForCommit,
            BuildPartitionHeaders(partitionLabel, messageHeaders));

    // ──────────────────────────────────────────────────────────────────
    // Pull
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<PullContainer> Pull(PullRequest request,
        Func<int, HorseMessage, Task> actionForEachMessage = null)
        => _client.Queue.Pull(request, actionForEachMessage);

    // ──────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────

    private static IEnumerable<KeyValuePair<string, string>> BuildPartitionHeaders(
        string partitionLabel,
        IEnumerable<KeyValuePair<string, string>> extra)
    {
        var headers = new List<KeyValuePair<string, string>>();

        if (!string.IsNullOrEmpty(partitionLabel))
            headers.Add(new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, partitionLabel));

        if (extra != null)
            headers.AddRange(extra);

        return headers;
    }
}