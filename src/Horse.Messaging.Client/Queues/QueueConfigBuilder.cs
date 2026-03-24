using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Interceptors;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

/// <summary>
/// Builder for queue configuration
/// </summary>
public class QueueConfigBuilder
{
    private string _moveOnErrorQueueName;
    private string _moveOnErrorQueueTopic;
    private TransportExceptionDescriptor _defaultPushException;
    private TransportExceptionDescriptor _defaultPublishException;

    /// <summary>
    /// If true, message is sent as high priority.
    /// This setting is not supported for queue consumer registration.
    /// </summary>
    public bool HighPriority { get; set; }

    /// <summary>
    /// If not null, subscribe uses partitioned mode with this label.
    /// Empty string = label-less partitioned subscribe.
    /// Null = plain subscribe.
    /// </summary>
    public string PartitionLabel { get; set; }

    /// <summary>
    /// Maximum partition count for auto-create. null = not set (server default), 0 = unlimited.
    /// </summary>
    public int? MaxPartitions { get; set; }

    /// <summary>
    /// Max subscribers per partition for auto-create. null = not set (server default).
    /// </summary>
    public int? SubscribersPerPartition { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, that option will be used
    /// </summary>
    public QueueAckDecision? Acknowledge { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, queue will be created with that status
    /// </summary>
    public MessagingQueueType? QueueType { get; set; }

    /// <summary>
    /// If queue is created with a message push and that value is not null, queue topic.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Headers for delivery descriptor of type
    /// </summary>
    public List<KeyValuePair<string, string>> Headers { get; } = new List<KeyValuePair<string, string>>();

    /// <summary>
    /// Queue name for queue messages
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Delay between messages option (in milliseconds)
    /// </summary>
    public int? DelayBetweenMessages { get; set; }

    /// <summary>
    /// Put back decision
    /// </summary>
    public PutBack? PutBackDecision { get; set; }

    /// <summary>
    /// Put back delay in milliseconds
    /// </summary>
    public int? PutBackDelay { get; set; }

    /// <summary>
    /// Message timeout in seconds
    /// </summary>
    public MessageTimeoutStrategyInfo MessageTimeout { get; set; }

    /// <summary>
    /// Acknowledge timeout in seconds
    /// </summary>
    public int? AcknowledgeTimeout { get; set; }

    /// <summary>
    /// If true, server checks all message id values and reject new messages with same id.
    /// Enabling that feature has performance penalty about 0.03 ms for each message.
    /// </summary>
    public bool? UniqueIdCheck { get; set; }

    internal List<InterceptorTypeDescriptor> Interceptors { get; } = new List<InterceptorTypeDescriptor>();
    internal List<TransportExceptionDescriptor> PushExceptionDescriptors { get; } = new List<TransportExceptionDescriptor>();
    internal List<TransportExceptionDescriptor> PublishExceptionDescriptors { get; } = new List<TransportExceptionDescriptor>();

    /// <summary>
    /// Uses interceptor for the queue
    /// </summary>
    public void UseInterceptor<T>(int order = 0, bool runBefore = true) where T : class, IHorseInterceptor
    {
        InterceptorAttribute attr = new InterceptorAttribute(typeof(T), order, runBefore);
        Interceptors.Add(InterceptorTypeDescriptor.Create(attr, true));
    }

    /// <summary>
    /// Moves failed messages to another queue with exception metadata in additional content.
    /// </summary>
    /// <param name="queueName">Target error queue name.</param>
    /// <param name="topicName">Optional topic for auto-created error queue.</param>
    public void MoveOnError(string queueName, string topicName = null)
    {
        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));

        _moveOnErrorQueueName = queueName;
        _moveOnErrorQueueTopic = topicName;
    }

    /// <summary>
    /// Pushes thrown exceptions to the queue resolved from the exception model type.
    /// </summary>
    public void PushExceptions<TExceptionModel>(Type exceptionType = null)
        where TExceptionModel : ITransportableException, new()
    {
        AddTransportExceptionDescriptor(typeof(TExceptionModel), exceptionType, ref _defaultPushException, PushExceptionDescriptors);
    }

    /// <summary>
    /// Publishes thrown exceptions to the router resolved from the exception model type.
    /// </summary>
    public void PublishExceptions<TExceptionModel>(Type exceptionType = null)
        where TExceptionModel : ITransportableException, new()
    {
        AddTransportExceptionDescriptor(typeof(TExceptionModel), exceptionType, ref _defaultPublishException, PublishExceptionDescriptors);
    }

    internal QueueTypeDescriptor Build()
    {
        if (HighPriority)
            throw new NotSupportedException("HighPriority is not supported for queue consumer registration.");

        QueueTypeDescriptor descriptor = new QueueTypeDescriptor
        {
            HighPriority = HighPriority,
            PartitionLabel = PartitionLabel,
            MaxPartitions = MaxPartitions,
            SubscribersPerPartition = SubscribersPerPartition,
            QueueType = QueueType,
            Acknowledge = Acknowledge,
            Topic = Topic,
            QueueName = QueueName,
            DelayBetweenMessages = DelayBetweenMessages,
            PutBackDecision = PutBackDecision,
            PutBackDelay = PutBackDelay,
            MessageTimeout = MessageTimeout,
            AcknowledgeTimeout = AcknowledgeTimeout,
            UniqueIdCheck = UniqueIdCheck,
            MoveOnErrorQueueName = _moveOnErrorQueueName,
            MoveOnErrorQueueTopic = _moveOnErrorQueueTopic,
            DefaultPushException = _defaultPushException,
            DefaultPublishException = _defaultPublishException
        };

        descriptor.Headers.AddRange(Headers);
        descriptor.Interceptors.AddRange(Interceptors);
        descriptor.PushExceptions.AddRange(PushExceptionDescriptors);
        descriptor.PublishExceptions.AddRange(PublishExceptionDescriptors);

        return descriptor;
    }

    private static void AddTransportExceptionDescriptor(Type modelType, Type exceptionType,
        ref TransportExceptionDescriptor defaultDescriptor, List<TransportExceptionDescriptor> descriptors)
    {
        if (exceptionType == null)
        {
            defaultDescriptor = new TransportExceptionDescriptor(modelType);
            return;
        }

        if (!typeof(Exception).IsAssignableFrom(exceptionType))
            throw new ArgumentException("Exception type must derive from System.Exception.", nameof(exceptionType));

        descriptors.RemoveAll(x => x.ExceptionType == exceptionType);
        descriptors.Add(new TransportExceptionDescriptor(modelType, exceptionType));
    }
}
