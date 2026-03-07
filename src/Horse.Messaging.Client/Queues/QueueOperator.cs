using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Exceptions;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Handler for queue name generator
/// </summary>
public delegate string QueueNameHandler(QueueNameHandlerContext context);

/// <summary>
/// Queue manager object for Horse client
/// </summary>
public class QueueOperator : IDisposable
{
    internal HorseClient Client { get; }
    private readonly Timer _pullContainerTimeoutHandler;

    internal TypeDescriptorContainer<QueueTypeDescriptor> DescriptorContainer { get; }
    internal List<QueueConsumerRegistration> Registrations { get; private set; } = new();
    internal Dictionary<string, PullContainer> PullContainers { get; }

    private int _activeConsumeOperations;

    /// <summary>
    /// Returns count of consume operations
    /// </summary>
    public int ActiveConsumeOperations => _activeConsumeOperations;

    /// <summary>
    /// Queue name handler
    /// </summary>
    public QueueNameHandler NameHandler { get; set; }

    internal QueueOperator(HorseClient client)
    {
        Client = client;
        DescriptorContainer = new TypeDescriptorContainer<QueueTypeDescriptor>(new QueueTypeResolver(client));
        PullContainers = new Dictionary<string, PullContainer>();
        _pullContainerTimeoutHandler = new Timer(HandleTimeoutPulls, null, 1000, 1000);
    }

    internal async Task OnQueueMessage(HorseMessage message)
    {
        //if message is response for pull request, process pull container
        if (PullContainers.Count > 0 && message.HasHeader)
        {
            string requestId = message.FindHeader(HorseHeaders.REQUEST_ID);
            if (!string.IsNullOrEmpty(requestId))
            {
                PullContainer container;
                lock (PullContainers)
                    PullContainers.TryGetValue(requestId, out container);

                if (container != null)
                {
                    ProcessPull(requestId, message, container);
                    return;
                }
            }
        }

        //consume push state queue message
        QueueConsumerRegistration reg = Registrations.FirstOrDefault(x => x.QueueName == message.Target);

        // If not found by exact name, check whether this is a partition sub-queue message
        // (e.g. "FetchOrders-Partition-a3k9x" → look up by parent queue name "FetchOrders").
        if (reg == null && message.Target != null)
        {
            const string partitionSuffix = "-Partition-";
            int idx = message.Target.LastIndexOf(partitionSuffix, StringComparison.Ordinal);
            if (idx > 0)
            {
                string parentQueueName = message.Target.Substring(0, idx);
                reg = Registrations.FirstOrDefault(x => x.QueueName == parentQueueName);
            }
        }

        if (reg == null)
            return;

        object model = reg.MessageType == typeof(string)
            ? message.GetStringContent()
            : Client.MessageSerializer.Deserialize(message, reg.MessageType);

        try
        {
            Interlocked.Increment(ref _activeConsumeOperations);
            await reg.ConsumerExecuter.Execute(Client, message, model, Client.ConsumeToken);
        }
        catch (Exception ex)
        {
            Client.OnException(ex, message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeConsumeOperations);
        }
    }

    /// <summary>
    /// Processes pull message
    /// </summary>
    private void ProcessPull(string requestId, HorseMessage message, PullContainer container)
    {
        if (message.Length > 0)
            container.AddMessage(message);

        string noContent = message.FindHeader(HorseHeaders.NO_CONTENT);

        if (!string.IsNullOrEmpty(noContent))
        {
            lock (PullContainers)
                PullContainers.Remove(requestId);

            container.Complete(noContent);
        }
    }

    /// <summary>
    /// Handles timed out pull requests and removed them
    /// </summary>
    private void HandleTimeoutPulls(object state)
    {
        try
        {
            if (PullContainers.Count > 0)
            {
                List<PullContainer> timedouts = new List<PullContainer>();
                lock (PullContainers)
                {
                    foreach (PullContainer container in PullContainers.Values)
                    {
                        if (container.Status == PullProcess.Receiving && container.LastReceived + Client.PullTimeout < DateTime.UtcNow)
                            timedouts.Add(container);
                    }
                }

                foreach (PullContainer container in timedouts)
                {
                    lock (PullContainers)
                        PullContainers.Remove(container.RequestId);

                    container.Complete(null);
                }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Releases all resources
    /// </summary>
    public void Dispose()
    {
        _pullContainerTimeoutHandler?.Dispose();
    }

    #region Actions

    /// <summary>
    /// Creates a new queue on the server with default options.
    /// </summary>
    /// <param name="queue">Queue name to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Create(string queue, CancellationToken cancellationToken = default)
    {
        return Create(queue, null, null, null, cancellationToken);
    }

    /// <summary>
    /// Creates a new queue on the server with additional headers.
    /// </summary>
    /// <param name="queue">Queue name to create.</param>
    /// <param name="additionalHeaders">Additional headers to include in the create request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Create(string queue,
        IEnumerable<KeyValuePair<string, string>> additionalHeaders,
        CancellationToken cancellationToken = default)
    {
        return Create(queue, null, null, additionalHeaders, cancellationToken);
    }

    /// <summary>
    /// Creates a new queue on the server with a specific queue manager.
    /// </summary>
    /// <param name="queue">Queue name to create.</param>
    /// <param name="deliveryHandlerHeader">Queue manager name header value.</param>
    /// <param name="additionalHeaders">Additional headers to include in the create request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Create(string queue,
        string deliveryHandlerHeader,
        IEnumerable<KeyValuePair<string, string>> additionalHeaders,
        CancellationToken cancellationToken = default)
    {
        return Create(queue, null, deliveryHandlerHeader, additionalHeaders, cancellationToken);
    }

    /// <summary>
    /// Creates a new queue on the server with full control over options, queue manager, and headers.
    /// </summary>
    /// <param name="queue">Queue name to create.</param>
    /// <param name="optionsAction">Action to configure queue options. Null to use server defaults.</param>
    /// <param name="queueManagerName">Queue manager name. Null to use the default manager.</param>
    /// <param name="additionalHeaders">Additional headers to include in the create request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Create(string queue,
        Action<QueueOptions> optionsAction,
        string queueManagerName,
        IEnumerable<KeyValuePair<string, string>> additionalHeaders,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.CreateQueue;
        message.SetTarget(queue);
        message.WaitResponse = true;
        message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

        if (!string.IsNullOrEmpty(queueManagerName))
            message.AddHeader(HorseHeaders.QUEUE_MANAGER, queueManagerName);

        if (additionalHeaders != null)
            foreach (KeyValuePair<string, string> pair in additionalHeaders)
                message.AddHeader(pair.Key, pair.Value);

        if (optionsAction != null)
        {
            QueueOptions options = new QueueOptions();
            optionsAction(options);

            message.Content = new MemoryStream();
            await JsonSerializer.SerializeAsync(message.Content, options, SerializerFactory.Default());
        }

        message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Lists all queues on the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseModelResult<List<QueueInformation>>> List(CancellationToken cancellationToken = default)
    {
        return List(null, cancellationToken);
    }

    /// <summary>
    /// Lists queues on the server matching a filter pattern.
    /// </summary>
    /// <param name="filter">Filter pattern (supports wildcards). Null returns all queues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseModelResult<List<QueueInformation>>> List(string filter, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.SetMessageId(Client.UniqueIdGenerator.Create());
        message.ContentType = KnownContentTypes.QueueList;

        if (!string.IsNullOrEmpty(filter))
            message.AddHeader(HorseHeaders.FILTER, filter);

        return await Client.SendAsync<List<QueueInformation>>(message, cancellationToken);
    }

    /// <summary>
    /// Removes a queue from the server.
    /// </summary>
    /// <param name="queue">Queue name to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Remove(string queue, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.RemoveQueue;
        message.SetTarget(queue);
        message.WaitResponse = true;
        message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Updates options for an existing queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="optionsAction">Action to configure the new queue options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> SetOptions(string queue, Action<QueueOptions> optionsAction, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.UpdateQueue;
        message.SetTarget(queue);
        message.WaitResponse = true;
        message.SetMessageId(Client.UniqueIdGenerator.Create());
        message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

        QueueOptions options = new QueueOptions();
        optionsAction(options);

        message.Content = new MemoryStream();
        await JsonSerializer.SerializeAsync(message.Content, options, SerializerFactory.Default());

        return await Client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Clears messages in a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="clearPriorityMessages">If true, clears high-priority messages.</param>
    /// <param name="clearMessages">If true, clears default-priority messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> ClearMessages(string queue, bool clearPriorityMessages, bool clearMessages, CancellationToken cancellationToken = default)
    {
        if (!clearPriorityMessages && !clearMessages)
            return Task.FromResult(HorseResult.Failed());

        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.ClearMessages;
        message.SetTarget(queue);
        message.WaitResponse = true;
        message.SetMessageId(Client.UniqueIdGenerator.Create());
        message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

        if (clearPriorityMessages)
            message.AddHeader(HorseHeaders.PRIORITY_MESSAGES, "yes");

        if (clearMessages)
            message.AddHeader(HorseHeaders.MESSAGES, "yes");

        return Client.WaitResponse(message, true, cancellationToken);
    }

    /// <summary>
    /// Gets all consumers subscribed to a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseModelResult<List<ClientInformation>>> GetConsumers(string queue, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.SetTarget(queue);
        message.ContentType = KnownContentTypes.QueueConsumers;
        message.SetMessageId(Client.UniqueIdGenerator.Create());

        message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

        return await Client.SendAsync<List<ClientInformation>>(message, cancellationToken);
    }

    #endregion

    #region Push - Pull

    /// <summary>
    /// Pushes a serialized model into a queue without waiting for commit (fire-and-forget).
    /// The queue name is resolved from the <typeparamref name="T"/> attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, null, false, null, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model into a queue.
    /// The queue name is resolved from the <typeparamref name="T"/> attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, bool waitForCommit, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, null, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model into a queue with custom headers.
    /// The queue name is resolved from the model's attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, null, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model with an explicit message id into a queue.
    /// The queue name is resolved from the model's attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, messageId, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model with an explicit message id into a queue with custom headers.
    /// The queue name is resolved from the model's attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, messageId, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model into the specified queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(queue, model, null, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model into the specified queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(queue, model, null, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model into the specified queue with an explicit message id.
    /// This is the canonical overload; all other model Push variants delegate to this method.
    /// </summary>
    public async Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default) where T : class
    {
        Type runtimeType = model.GetType();
        QueueTypeDescriptor descriptor = DescriptorContainer.GetDescriptor(runtimeType);

        string resolvedQueue = descriptor.QueueName;

        if (!string.IsNullOrEmpty(queue))
            resolvedQueue = queue;

        if (NameHandler != null)
        {
            string handlerResult = NameHandler.Invoke(new QueueNameHandlerContext
            {
                Client = Client,
                Type = runtimeType,
                QueueName = resolvedQueue
            });

            if (!string.IsNullOrEmpty(handlerResult))
                resolvedQueue = handlerResult;
        }

        HorseMessage message = descriptor.CreateMessage(resolvedQueue);

        if (!string.IsNullOrEmpty(messageId))
            message.SetMessageId(messageId);

        message.WaitResponse = waitForCommit;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        message.Serialize(model, Client.MessageSerializer);

        if (string.IsNullOrEmpty(message.MessageId) && waitForCommit)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, waitForCommit, cancellationToken);
    }

    /// <summary>
    /// Pushes an object model to a queue. Used internally when the compile-time type is not available.
    /// </summary>
    internal Task<HorseResult> PushObject(object model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        return PushObject(null, model, null, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes an object model to a queue. Used internally when the compile-time type is not available.
    /// </summary>
    internal async Task<HorseResult> PushObject(string queue, object model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        Type runtimeType = model.GetType();
        QueueTypeDescriptor descriptor = DescriptorContainer.GetDescriptor(runtimeType);

        string resolvedQueue = descriptor.QueueName;

        if (!string.IsNullOrEmpty(queue))
            resolvedQueue = queue;

        if (NameHandler != null)
        {
            string handlerResult = NameHandler.Invoke(new QueueNameHandlerContext
            {
                Client = Client,
                Type = runtimeType,
                QueueName = resolvedQueue
            });

            if (!string.IsNullOrEmpty(handlerResult))
                resolvedQueue = handlerResult;
        }

        HorseMessage message = descriptor.CreateMessage(resolvedQueue);

        if (!string.IsNullOrEmpty(messageId))
            message.SetMessageId(messageId);

        message.WaitResponse = waitForCommit;

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        message.Serialize(model, Client.MessageSerializer);

        if (string.IsNullOrEmpty(message.MessageId) && waitForCommit)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, waitForCommit, cancellationToken);
    }

    #endregion

    #region Push — partitionLabel convenience

    /// <summary>
    /// Pushes a serialized model into a queue with custom headers and partition label.
    /// The queue name is resolved from the model's attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, null, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model into the specified queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(string queue, T model, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(queue, model, null, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model with an explicit message id into a queue with custom headers and partition label.
    /// The queue name is resolved from the model's attribute.
    /// </summary>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(null, model, messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes a serialized model with an explicit message id into the specified queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="model">The message model to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push<T>(string queue, T model, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default) where T : class
    {
        return Push<T>(queue, model, messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content into a queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default)
    {
        return Push(queue, content, null, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content with an explicit message id into a queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default)
    {
        return Push(queue, content, messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes raw byte array content into a queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), null, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

    /// <summary>
    /// Pushes raw byte array content with an explicit message id into a queue with custom headers and partition label.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="partitionLabel">Partition label for partition-aware routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, string partitionLabel, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), messageId, waitForCommit, MergePartitionHeader(partitionLabel, messageHeaders), cancellationToken);
    }

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

    #endregion

    #region PushBulk

    /// <summary>
    /// Pushes multiple models to a queue. Queue name is resolved from the runtime type of the first item.
    /// </summary>
    /// <param name="items">List of model instances to push.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    public void PushBulk<T>(List<T> items, Action<HorseMessage, bool> callback) where T : class
    {
        PushBulk(null, items, callback, null);
    }

    /// <summary>
    /// Pushes multiple models to a queue with custom headers. Queue name is resolved from the runtime type of the first item.
    /// </summary>
    /// <param name="items">List of model instances to push.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    /// <param name="messageHeaders">Additional message headers applied to all messages.</param>
    public void PushBulk<T>(List<T> items, Action<HorseMessage, bool> callback, IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
    {
        PushBulk(null, items, callback, messageHeaders);
    }

    /// <summary>
    /// Pushes multiple models to a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="items">List of model instances to push.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    public void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback) where T : class
    {
        PushBulk(queue, items, callback, null);
    }

    /// <summary>
    /// Pushes multiple models to a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="items">List of model instances to push.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    /// <param name="messageHeaders">Additional message headers applied to all messages.</param>
    public void PushBulk<T>(string queue, List<T> items, Action<HorseMessage, bool> callback, IEnumerable<KeyValuePair<string, string>> messageHeaders) where T : class
    {
        if (items == null || items.Count == 0)
            return;

        Type runtimeType = items[0].GetType();
        QueueTypeDescriptor descriptor = DescriptorContainer.GetDescriptor(runtimeType);

        string resolvedQueue = descriptor.QueueName;

        if (!string.IsNullOrEmpty(queue))
            resolvedQueue = queue;

        if (NameHandler != null)
        {
            string handlerResult = NameHandler.Invoke(new QueueNameHandlerContext
            {
                Client = Client,
                Type = runtimeType,
                QueueName = resolvedQueue
            });

            if (!string.IsNullOrEmpty(handlerResult))
                resolvedQueue = handlerResult;
        }

        HorseMessage firstMessage = descriptor.CreateMessage(resolvedQueue);
        firstMessage.WaitResponse = true;
        firstMessage.SetMessageId(Client.UniqueIdGenerator.Create());

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                firstMessage.AddHeader(pair.Key, pair.Value);

        firstMessage.Serialize(items[0], Client.MessageSerializer);

        List<HorseMessage> messages = new List<HorseMessage>(items.Count);
        messages.Add(firstMessage);

        for (int i = 1; i < items.Count; i++)
        {
            HorseMessage msg = firstMessage.Clone(true, false, Client.UniqueIdGenerator.Create());
            msg.SetMessageId(Client.UniqueIdGenerator.Create());
            msg.Serialize(items[i], Client.MessageSerializer);
            messages.Add(msg);
        }

        if (callback != null)
            Client.Tracker.TrackMultiple(messages, callback);

        Client.SendBulk(messages, null);
    }

    /// <summary>
    /// Pushes raw binary content into a queue without waiting for commit (fire-and-forget).
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), null, false, null, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), null, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content into a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), null, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content with an explicit message id into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), messageId, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content with an explicit message id into a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="data">Raw byte array content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, byte[] data, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        return Push(queue, new MemoryStream(data), messageId, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content into a queue without waiting for commit (fire-and-forget).
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, MemoryStream content, CancellationToken cancellationToken = default)
    {
        return Push(queue, content, null, false, null, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit, CancellationToken cancellationToken = default)
    {
        return Push(queue, content, null, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content into a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        return Push(queue, content, null, waitForCommit, messageHeaders, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content with an explicit message id into a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit, CancellationToken cancellationToken = default)
    {
        return Push(queue, content, messageId, waitForCommit, null, cancellationToken);
    }

    /// <summary>
    /// Pushes raw binary content with an explicit message id into a queue with custom headers.
    /// This is the canonical raw Push overload; all other raw Push variants delegate to this method.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="content">Raw binary content to push.</param>
    /// <param name="messageId">Explicit unique message id.</param>
    /// <param name="waitForCommit">If true, waits for a commit response from the server.</param>
    /// <param name="messageHeaders">Additional message headers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
        IEnumerable<KeyValuePair<string, string>> messageHeaders, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, queue, 0);
        message.Content = content;
        message.WaitResponse = waitForCommit;

        if (!string.IsNullOrEmpty(messageId))
            message.SetMessageId(messageId);

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                message.AddHeader(pair.Key, pair.Value);

        if (string.IsNullOrEmpty(message.MessageId) && waitForCommit)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, waitForCommit, cancellationToken);
    }

    /// <summary>
    /// Pushes multiple raw binary messages to a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="contents">List of raw binary contents to push.</param>
    /// <param name="waitForCommit">If true, tracks commit responses from the server.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    public void PushBulk(string queue, List<MemoryStream> contents,
        bool waitForCommit, Action<HorseMessage, bool> callback)
    {
        PushBulk(queue, contents, waitForCommit, callback, null);
    }

    /// <summary>
    /// Pushes multiple raw binary messages to a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="contents">List of raw binary contents to push.</param>
    /// <param name="waitForCommit">If true, tracks commit responses from the server.</param>
    /// <param name="callback">Optional callback invoked per message with the commit result.</param>
    /// <param name="messageHeaders">Additional message headers applied to all messages.</param>
    public void PushBulk(string queue, List<MemoryStream> contents,
        bool waitForCommit, Action<HorseMessage, bool> callback,
        IEnumerable<KeyValuePair<string, string>> messageHeaders)
    {
        if (contents == null || contents.Count == 0)
            return;

        HorseMessage firstMessage = new HorseMessage(MessageType.QueueMessage, queue, 0);
        firstMessage.Content = contents.FirstOrDefault();
        firstMessage.WaitResponse = waitForCommit;

        if (string.IsNullOrEmpty(firstMessage.MessageId) && waitForCommit)
            firstMessage.SetMessageId(Client.UniqueIdGenerator.Create());

        if (messageHeaders != null)
            foreach (KeyValuePair<string, string> pair in messageHeaders)
                firstMessage.AddHeader(pair.Key, pair.Value);

        List<HorseMessage> messages = new List<HorseMessage>(contents.Count);
        messages.Add(firstMessage);

        for (int i = 1; i < contents.Count; i++)
        {
            HorseMessage msg = firstMessage.Clone(true, false, Client.UniqueIdGenerator.Create());
            msg.Content = contents[i];
            messages.Add(msg);
        }

        if (waitForCommit && callback != null)
            Client.Tracker.TrackMultiple(messages, callback);

        Client.SendBulk(messages, waitForCommit ? null : callback);
    }

    /// <summary>
    /// Sends a pull request to retrieve messages from a Pull-type queue.
    /// </summary>
    /// <param name="request">Pull request parameters (queue name, count, order, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PullContainer> Pull(PullRequest request, CancellationToken cancellationToken = default)
    {
        return Pull(request, null, cancellationToken);
    }

    /// <summary>
    /// Sends a pull request and invokes an action for each received message.
    /// </summary>
    /// <param name="request">Pull request parameters (queue name, count, order, etc.).</param>
    /// <param name="actionForEachMessage">Action invoked for each message. The int parameter is the 1-based index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage,
        CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage(MessageType.QueuePullRequest, request.Queue);
        message.SetMessageId(Client.UniqueIdGenerator.Create());
        message.AddHeader(HorseHeaders.COUNT, request.Count);

        if (request.ClearAfter == ClearDecision.AllMessages)
            message.AddHeader(HorseHeaders.CLEAR, "all");
        else if (request.ClearAfter == ClearDecision.PriorityMessages)
            message.AddHeader(HorseHeaders.CLEAR, "high-priority");
        else if (request.ClearAfter == ClearDecision.Messages)
            message.AddHeader(HorseHeaders.CLEAR, "default-priority");

        if (request.GetQueueMessageCounts)
            message.AddHeader(HorseHeaders.INFO, "yes");

        if (request.Order == MessageOrder.LIFO)
            message.AddHeader(HorseHeaders.ORDER, HorseHeaders.LIFO);

        foreach (KeyValuePair<string, string> pair in request.RequestHeaders)
            message.AddHeader(pair.Key, pair.Value);

        PullContainer container = new PullContainer(message.MessageId, request.Count, actionForEachMessage);
        lock (PullContainers)
            PullContainers.Add(message.MessageId, container);

        HorseResult sent = await Client.SendAsync(message, cancellationToken);
        if (sent.Code != HorseResultCode.Ok)
        {
            lock (PullContainers)
                PullContainers.Remove(message.MessageId);

            container.Complete("Error");
        }

        return await container.GetAwaitableTask(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Join - Leave

    /// <summary>
    /// Subscribes to a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Subscribe(string queue, bool verifyResponse, CancellationToken cancellationToken = default)
    {
        return Subscribe(queue, verifyResponse, null, cancellationToken);
    }

    /// <summary>
    /// Subscribes to a queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="headers">Additional headers to include in the subscription request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Subscribe(string queue, bool verifyResponse,
        IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.QueueSubscribe;
        message.SetTarget(queue);
        message.WaitResponse = verifyResponse;

        if (headers != null)
            foreach (KeyValuePair<string, string> header in headers)
                message.AddHeader(header.Key, header.Value);

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Subscribes to a partitioned queue.
    /// </summary>
    public Task<HorseResult> SubscribePartitioned(
        string queue,
        string partitionLabel,
        bool verifyResponse,
        CancellationToken cancellationToken = default)
    {
        return SubscribePartitioned(queue, partitionLabel, verifyResponse, null, null, null, cancellationToken);
    }

    /// <summary>
    /// Subscribes to a partitioned queue.
    /// </summary>
    public Task<HorseResult> SubscribePartitioned(
        string queue,
        string partitionLabel,
        bool verifyResponse,
        int? maxPartitions,
        int? subscribersPerPartition,
        CancellationToken cancellationToken = default)
    {
        return SubscribePartitioned(queue, partitionLabel, verifyResponse, maxPartitions, subscribersPerPartition, null, cancellationToken);
    }

    /// <summary>
    /// Subscribes to a partitioned queue.
    /// </summary>
    public Task<HorseResult> SubscribePartitioned(
        string queue,
        string partitionLabel,
        bool verifyResponse,
        int? maxPartitions,
        int? subscribersPerPartition,
        IEnumerable<KeyValuePair<string, string>> additionalHeaders,
        CancellationToken cancellationToken = default)
    {
        var headers = new List<KeyValuePair<string, string>>();

        if (!string.IsNullOrEmpty(partitionLabel))
            headers.Add(new KeyValuePair<string, string>(HorseHeaders.PARTITION_LABEL, partitionLabel));

        if (maxPartitions.HasValue)
            headers.Add(new KeyValuePair<string, string>(HorseHeaders.PARTITION_LIMIT, maxPartitions.Value.ToString()));

        if (subscribersPerPartition is > 0)
            headers.Add(new KeyValuePair<string, string>(HorseHeaders.PARTITION_SUBSCRIBERS, subscribersPerPartition.Value.ToString()));

        if (additionalHeaders != null)
            headers.AddRange(additionalHeaders);

        return Subscribe(queue, verifyResponse, headers, cancellationToken);
    }

    /// <summary>
    /// Re-registers the consumer type for another queue name and subscribes to the queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Subscribe<TConsumer, TModel>(string queue, bool verifyResponse, CancellationToken cancellationToken = default)
        where TConsumer : IQueueConsumer<TModel>
    {
        return Subscribe<TConsumer, TModel>(queue, verifyResponse, null, cancellationToken);
    }

    /// <summary>
    /// Re-registers the consumer type for another queue name and subscribes to the queue with custom headers.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="headers">Additional headers to include in the subscription request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Subscribe<TConsumer, TModel>(string queue, bool verifyResponse,
        IEnumerable<KeyValuePair<string, string>> headers, CancellationToken cancellationToken = default)
        where TConsumer : IQueueConsumer<TModel>
    {
        List<QueueConsumerRegistration> list = Registrations.ToList();
        QueueConsumerRegistration current = list.FirstOrDefault(x => x.ConsumerType == typeof(TConsumer));

        if (current == null)
            throw new HorseQueueException($"{typeof(TConsumer)} Consumer type is not registered");

        QueueConsumerRegistration registration = new QueueConsumerRegistration
        {
            ConsumerExecuter = current.ConsumerExecuter,
            ConsumerType = current.ConsumerType,
            MessageType = current.MessageType,
            QueueName = queue,
            PartitionLabel = current.PartitionLabel,
            MaxPartitions = current.MaxPartitions,
            SubscribersPerPartition = current.SubscribersPerPartition
        };

        foreach (InterceptorTypeDescriptor descriptor in current.InterceptorDescriptors)
            registration.InterceptorDescriptors.Add(descriptor);

        list.Add(registration);
        Registrations = list;

        return Subscribe(queue, verifyResponse, headers, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from a queue.
    /// </summary>
    /// <param name="queue">Target queue name.</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the unsubscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<HorseResult> Unsubscribe(string queue, bool verifyResponse, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.QueueUnsubscribe;
        message.SetTarget(queue);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return await Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from all queues on the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> UnsubscribeFromAllQueues(CancellationToken cancellationToken = default)
    {
        return Unsubscribe("*", true, cancellationToken);
    }

    #endregion
}
