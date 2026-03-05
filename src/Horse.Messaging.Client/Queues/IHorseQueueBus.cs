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
    #region Push — raw

    /// <inheritdoc cref="Push(string, MemoryStream, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content)
        => Push(queue, content, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue without waiting for commit.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, bool, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit)
        => Push(queue, content, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => Push(queue, content, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with custom headers and partition label.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, string, bool, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit)
        => Push(queue, content, messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, CancellationToken cancellationToken);

    /// <inheritdoc cref="Push(string, MemoryStream, string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel)
        => Push(queue, content, messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id, custom headers and partition label.
    /// </summary>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);

    #endregion

    #region Push — model

    /// <inheritdoc cref="Push{T}(T, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model) where T : class
        => Push(model, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the queue resolved from the model's QueueName attribute without waiting for commit.
    /// </summary>
    Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, bool waitForCommit) where T : class
        => Push(model, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the queue resolved from the model's QueueName attribute.
    /// </summary>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Pushes a model with a partition label, without custom headers.
    /// </summary>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, string partitionLabel) where T : class
        => Push(model, waitForCommit, null, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with a partition label, without custom headers.
    /// </summary>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, string partitionLabel, CancellationToken cancellationToken) where T : class
        => Push(model, waitForCommit, null, partitionLabel, cancellationToken);

    /// <inheritdoc cref="Push{T}(T, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(model, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with custom headers and partition label.
    /// </summary>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit) where T : class
        => Push(queue, model, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Pushes a model into the specified queue with a partition label, without custom headers.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, string partitionLabel) where T : class
        => Push(queue, model, waitForCommit, null, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue with a partition label, without custom headers.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, string partitionLabel, CancellationToken cancellationToken) where T : class
        => Push(queue, model, waitForCommit, null, partitionLabel, cancellationToken);

    /// <inheritdoc cref="Push{T}(string, T, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(queue, model, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model into the specified queue with custom headers and partition label.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, string, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit) where T : class
        => Push(model, messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id.
    /// </summary>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(T, string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(model, messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id, custom headers and partition label.
    /// </summary>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, string, bool, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit) where T : class
        => Push(queue, model, messageId, waitForCommit, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;

    /// <inheritdoc cref="Push{T}(string, T, string, bool, IEnumerable{KeyValuePair{string,string}}, string, CancellationToken)"/>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class
        => Push(queue, model, messageId, waitForCommit, messageHeaders, partitionLabel, CancellationToken.None);

    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue, with custom headers and partition label.
    /// </summary>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;

    #endregion

    #region PushBulk

    /// <summary>
    /// Pushes multiple model messages into a queue in a single batch.
    /// </summary>
    void PushBulk<T>(string queue, List<T> items) where T : class
        => PushBulk(queue, items, null, null, null);

    /// <summary>
    /// Pushes multiple model messages into a queue in a single batch with a callback.
    /// </summary>
    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback) where T : class
        => PushBulk(queue, items, callback, null, null);

    /// <summary>
    /// Pushes multiple model messages into a queue in a single batch.
    /// </summary>
    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class;

    /// <summary>
    /// Pushes multiple raw binary messages into a queue in a single batch.
    /// </summary>
    void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit)
        => PushBulk(queue, contents, waitForCommit, null, null, null);

    /// <summary>
    /// Pushes multiple raw binary messages into a queue in a single batch.
    /// </summary>
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
    Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken);

    /// <inheritdoc cref="Pull(PullRequest, Func{int, HorseMessage, Task}, CancellationToken)"/>
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage)
        => Pull(request, actionForEachMessage, CancellationToken.None);

    /// <summary>
    /// Sends a pull request and invokes the action for each received message.
    /// </summary>
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken);

    #endregion
}
