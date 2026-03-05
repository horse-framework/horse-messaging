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
/// </summary>
public interface IHorseQueueBus : IHorseConnection
{
    // ── Push — raw ──

    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken);
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, CancellationToken cancellationToken);
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);

    // ── Push — model ──

    Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    // ── PushBulk ──

    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class;

    void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit,
        Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel);

    // ── Pull ──

    Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken);
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken);
}
