using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

/// <inheritdoc />
public interface IHorseQueueBus<TIdentifier> : IHorseQueueBus
{
}

/// <summary>
/// Implementation for queue messages and requests
/// </summary>
public interface IHorseQueueBus : IHorseConnection
{
    // ──────────────────────────────────────────────────────────────────
    // Push
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Pushes a message into a queue.</summary>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>Pushes a message into a queue.</summary>
    Task<HorseResult> Push(string queue, string content, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>Pushes a message into a queue.</summary>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>Pushes a message into a queue.</summary>
    Task<HorseResult> Push(string queue, string content, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    // ──────────────────────────────────────────────────────────────────
    // PushJson
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Pushes a JSON message into a queue. Queue name is resolved from the type's [QueueName] attribute.</summary>
    Task<HorseResult> PushJson(object jsonObject, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>Pushes a JSON message into a specified queue.</summary>
    Task<HorseResult> PushJson(string queue, object jsonObject, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>Pushes a JSON message into a queue with an explicit message id.</summary>
    Task<HorseResult> PushJson(object jsonObject, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>Pushes a JSON message into a specified queue with an explicit message id.</summary>
    Task<HorseResult> PushJson(string queue, object jsonObject, string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    // ──────────────────────────────────────────────────────────────────
    // Partition Push  — separate method names are required because
    // Push(string queue, string partitionLabel, ...) and
    // Push(string queue, string content, ...) have identical signatures
    // and cannot be distinguished by the C# overload resolver.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes a message into a specific partition of a queue identified by <paramref name="partitionLabel"/>.
    /// Pass <c>null</c> for label-less routing (orphan or round-robin depending on server config).
    /// </summary>
    Task<HorseResult> PushToPartition(string queue, string partitionLabel, MemoryStream content,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <inheritdoc cref="PushToPartition(string,string,MemoryStream,bool,IEnumerable{KeyValuePair{string,string}})"/>
    Task<HorseResult> PushToPartition(string queue, string partitionLabel, string content,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <inheritdoc cref="PushToPartition(string,string,MemoryStream,bool,IEnumerable{KeyValuePair{string,string}})"/>
    Task<HorseResult> PushToPartition(string queue, string partitionLabel, MemoryStream content,
        string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <inheritdoc cref="PushToPartition(string,string,MemoryStream,bool,IEnumerable{KeyValuePair{string,string}})"/>
    Task<HorseResult> PushToPartition(string queue, string partitionLabel, string content,
        string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    // ──────────────────────────────────────────────────────────────────
    // Partition PushJson  — same rationale: PushJson(string, object)
    // vs PushJson(string partitionLabel, object) are identical signatures.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes a JSON-serialised object into a specific partition of a queue.
    /// Queue name is resolved from the type's <c>[QueueName]</c> attribute.
    /// </summary>
    Task<HorseResult> PushJsonToPartition(string partitionLabel, object jsonObject,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <summary>
    /// Pushes a JSON-serialised object into a specific partition of the given queue.
    /// </summary>
    Task<HorseResult> PushJsonToPartition(string queue, string partitionLabel, object jsonObject,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <inheritdoc cref="PushJsonToPartition(string,object,bool,IEnumerable{KeyValuePair{string,string}})"/>
    Task<HorseResult> PushJsonToPartition(string partitionLabel, object jsonObject, string messageId,
        bool waitForCommit = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    /// <inheritdoc cref="PushJsonToPartition(string,string,object,bool,IEnumerable{KeyValuePair{string,string}})"/>
    Task<HorseResult> PushJsonToPartition(string queue, string partitionLabel, object jsonObject,
        string messageId, bool waitForCommit = false,
        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

    // ──────────────────────────────────────────────────────────────────
    // Pull
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Request a pull request.</summary>
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage = null);
}
