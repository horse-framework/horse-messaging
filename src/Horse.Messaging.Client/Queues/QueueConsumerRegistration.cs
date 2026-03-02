using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;

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
    /// auto-create. 0 = server default / not set.
    /// </summary>
    public int MaxPartitions { get; set; }

    /// <summary>
    /// Maximum subscribers per partition forwarded as <c>Partition-Subscribers</c> header
    /// during auto-create. 0 = server default / not set.
    /// </summary>
    public int SubscribersPerPartition { get; set; }

    /// <summary>Returns true when this registration uses partitioned subscribe.</summary>
    public bool IsPartitioned => PartitionLabel != null;
}