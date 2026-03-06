using System;

namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Represents a single active partition bound to a partitioned parent queue.
/// </summary>
public class PartitionEntry
{
    /// <summary>Short base-62 unique partition identifier (e.g. "a3f2k1").</summary>
    public string PartitionId { get; init; }

    /// <summary>
    /// Worker routing label assigned to this partition.
    /// Null for unlabeled partitions.
    /// </summary>
    public string Label { get; set; }

    /// <summary>The underlying HorseQueue instance for this partition.</summary>
    public HorseQueue Queue { get; init; }

    /// <summary>UTC time this partition was created.</summary>
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>UTC time of the last message routed to this partition.</summary>
    public DateTime? LastMessageAt { get; set; }
}
