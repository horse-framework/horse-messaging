using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;

namespace Horse.Messaging.Client.Queues;

internal class QueueConsumerRegistration
{
    /// <summary>
    /// Queue name
    /// </summary>
    public string QueueName { get; set; }
        
    /// <summary>
    /// Direct Consumer type
    /// </summary>
    public Type ConsumerType { get; set; }

    /// <summary>
    /// Direct message type
    /// </summary>
    public Type MessageType { get; set; }
        
    /// <summary>
    /// Interceptor descriptors
    /// </summary>
    internal List<InterceptorTypeDescriptor> InterceptorDescriptors { get; } = new();

    /// <summary>
    /// Headers used during queue subscribe / auto-create.
    /// </summary>
    internal List<KeyValuePair<string, string>> SubscriptionHeaders { get; } = new();

    /// <summary>
    /// Move-on-error configuration resolved from builder or attributes.
    /// </summary>
    internal MoveOnErrorAttribute MoveOnError { get; set; }

    /// <summary>
    /// True when the consumer should ACK automatically after successful execution.
    /// </summary>
    internal bool AutoAck { get; set; }

    /// <summary>
    /// True when the consumer should NACK automatically after failed execution.
    /// </summary>
    internal bool AutoNack { get; set; }

    /// <summary>
    /// NACK reason used when <see cref="AutoNack"/> is enabled.
    /// </summary>
    internal NegativeReason AutoNackReason { get; set; }

    /// <summary>
    /// Retry configuration resolved from builder or attributes.
    /// </summary>
    internal RetryAttribute Retry { get; set; }

    /// <summary>
    /// Default push-exception transport descriptor.
    /// </summary>
    internal TransportExceptionDescriptor DefaultPushException { get; set; }

    /// <summary>
    /// Additional push-exception descriptors.
    /// </summary>
    internal List<TransportExceptionDescriptor> PushExceptions { get; } = new();

    /// <summary>
    /// Default publish-exception transport descriptor.
    /// </summary>
    internal TransportExceptionDescriptor DefaultPublishException { get; set; }

    /// <summary>
    /// Additional publish-exception descriptors.
    /// </summary>
    internal List<TransportExceptionDescriptor> PublishExceptions { get; } = new();

    /// <summary>
    /// Consumer executor
    /// </summary>
    internal ExecutorBase ConsumerExecuter { get; set; }

    // ── Partition metadata ────────────────────────────────────────────────

    /// <summary>
    /// If not null, <see cref="QueueOperator.SubscribePartitioned"/> is called instead of
    /// plain <see cref="QueueOperator.Subscribe"/> during auto-subscribe.
    /// Sourced from <see cref="Annotations.PartitionedQueueAttribute"/> or the builder overload.
    /// Empty string means "label-less partitioned subscribe" (round-robin path).
    /// Null means "not partitioned" — plain subscribe is used.
    /// </summary>
    public string PartitionLabel { get; set; }

    /// <summary>
    /// Maximum number of partitions forwarded as <c>Partition-Limit</c> header during
    /// auto-create. null = not set (server default), 0 = unlimited, >0 = explicit limit.
    /// </summary>
    public int? MaxPartitions { get; set; }

    /// <summary>
    /// Maximum subscribers per partition forwarded as <c>Partition-Subscribers</c> header
    /// during auto-create. null = not set (server default), >0 = explicit value.
    /// </summary>
    public int? SubscribersPerPartition { get; set; }

    /// <summary>Returns true when this registration uses partitioned subscribe.</summary>
    public bool IsPartitioned => PartitionLabel != null;
}
