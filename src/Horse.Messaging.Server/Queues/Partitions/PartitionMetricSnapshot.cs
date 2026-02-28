using System;

namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Point-in-time metrics snapshot for a single partition.
/// Included in QueueInfo.PartitionMetrics when partitioning is enabled.
/// </summary>
public class PartitionMetricSnapshot
{
    public string PartitionId { get; init; }
    public string Label { get; init; }
    public bool IsOrphan { get; init; }
    public int MessageCount { get; init; }
    public int ConsumerCount { get; init; }
    public string QueueName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastMessageAt { get; init; }
}
