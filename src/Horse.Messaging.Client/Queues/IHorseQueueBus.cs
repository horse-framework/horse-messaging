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

    /// <summary>
    /// Pushes raw binary content into a queue without waiting for commit.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken);

    /// <summary>
    /// Pushes raw binary content into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken);
    /// <summary>
    /// Pushes raw binary content into a queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);
    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, CancellationToken cancellationToken);
    /// <summary>
    /// Pushes raw binary content into a queue with an explicit message id, custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken);
    #endregion
    #region Push — model

    /// <summary>
    /// Pushes a model into the queue resolved from the model's QueueName attribute without waiting for commit.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken) where T : class;

    /// <summary>
    /// Pushes a model into the queue resolved from the model's QueueName attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model into the queue resolved from the model's QueueName attribute with custom headers and partition label.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model into the specified queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model into the specified queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model with an explicit message id into the queue resolved from the model's QueueName attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model with an explicit message id into the queue resolved from the model's QueueName attribute, with custom headers and partition label.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit, CancellationToken cancellationToken) where T : class;
    /// <summary>
    /// Pushes a model with an explicit message id into the specified queue, with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server (see CommitWhen).</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken) where T : class;
    #endregion
    #region PushBulk
    /// <summary>
    /// Pushes multiple model messages into a queue in a single batch.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="items">List of model instances to push.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    /// <param name="messageHeaders">Additional message headers applied to all messages.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel) where T : class;
    /// <summary>
    /// Pushes multiple raw binary messages into a queue in a single batch.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="contents">List of raw binary contents to push.</param>
    /// <param name="waitForCommit">If true, tracks commit responses from the server.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    /// <param name="messageHeaders">Additional message headers applied to all messages.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    void PushBulk(string queue, List<MemoryStream> contents, bool waitForCommit,
        Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel);
    #endregion
    #region Pull
    /// <summary>
    /// Sends a pull request to a Pull-type queue and returns the pulled messages.
    /// </summary>
    /// <param name="request">Pull request parameters (queue name, count, order, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Sends a pull request and invokes the action for each received message.
    /// </summary>
    /// <param name="request">Pull request parameters (queue name, count, order, etc.).</param>
    /// <param name="actionForEachMessage">Action invoked for each message. The int parameter is the 1-based index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage, CancellationToken cancellationToken);
    #endregion
}
