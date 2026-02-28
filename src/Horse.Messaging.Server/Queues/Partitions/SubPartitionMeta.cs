namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Runtime metadata attached to a partition sub-queue (IsPartitionQueue = true).
/// Used to re-attach the queue to its parent PartitionManager on server restart.
/// </summary>
public class SubPartitionMeta
{
    /// <summary>Name of the parent (partitioned) queue.</summary>
    public string ParentQueueName { get; set; }

    /// <summary>Partition id (base-62 or "Orphan").</summary>
    public string PartitionId { get; set; }

    /// <summary>Worker routing label. Null for label-less or orphan partitions.</summary>
    public string Label { get; set; }

    /// <summary>True when this is the orphan (fallback) partition.</summary>
    public bool IsOrphan { get; set; }
}

