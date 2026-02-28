using System;

namespace Horse.Messaging.Client.Queues.Annotations;

/// <summary>
/// Configures a queue consumer to subscribe to a partitioned queue.
/// <para>
/// Apply to the <b>consumer class</b> (or the model class):
/// </para>
/// <code>
/// // Dedicated partition for tenant-42 (max 10 partitions, 1 subscriber each)
/// [PartitionedQueue("tenant-42", MaxPartitions = 10, SubscribersPerPartition = 1)]
/// public class FetchOrderConsumer : IQueueConsumer&lt;FetchOrderEvent&gt; { ... }
///
/// // Label-less partitioned subscribe (orphan / round-robin path)
/// [PartitionedQueue(MaxPartitions = 5)]
/// public class JobConsumer : IQueueConsumer&lt;JobEvent&gt; { ... }
/// </code>
/// <para>
/// When <see cref="HorseClient.AutoSubscribe"/> is <c>true</c> the client automatically
/// calls <see cref="QueueOperator.SubscribePartitioned"/> with the declared values on
/// every reconnect.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PartitionedQueueAttribute : Attribute
{
    /// <summary>
    /// Routing label sent to the server as <c>Partition-Label</c> header.
    /// <c>null</c> or empty = label-less partitioned subscribe (orphan / round-robin path).
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Maximum number of partitions forwarded as <c>Partition-Limit</c> header for
    /// auto-create. <c>0</c> = server default.
    /// </summary>
    public int MaxPartitions { get; init; }

    /// <summary>
    /// Maximum subscribers per partition forwarded as <c>Partition-Subscribers</c> header
    /// for auto-create. <c>0</c> = server default.
    /// </summary>
    public int SubscribersPerPartition { get; init; }

    /// <summary>
    /// Creates a new <see cref="PartitionedQueueAttribute"/> with an optional routing label.
    /// </summary>
    /// <param name="label">
    /// Partition routing label. Pass <c>null</c> or omit for label-less partitioned subscribe.
    /// </param>
    public PartitionedQueueAttribute(string label = null)
    {
        Label = label;
    }
}
