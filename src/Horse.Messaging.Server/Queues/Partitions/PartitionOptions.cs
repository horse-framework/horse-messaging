namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Partition configuration for a partitioned parent queue.
/// Attach to QueueOptions.Partition and set Enabled = true to activate partitioning.
/// </summary>
public class PartitionOptions
{
    /// <summary>
    /// Enables queue partitioning.
    /// When true the queue acts as a virtual router; messages are forwarded to partition sub-queues.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum number of partitions (excluding the orphan partition). 0 = unlimited.
    /// </summary>
    public int MaxPartitionCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of subscribers per partition (maps to ClientLimit on each partition queue).
    /// </summary>
    public int SubscribersPerPartition { get; set; } = 1;

    /// <summary>
    /// Auto-destroy rule evaluated for each individual partition.
    /// Does NOT affect the parent queue or the orphan partition.
    /// </summary>
    public PartitionAutoDestroy AutoDestroy { get; set; } = PartitionAutoDestroy.Disabled;

    /// <summary>
    /// Idle evaluation interval in seconds for the auto-destroy check.
    /// </summary>
    public int AutoDestroyIdleSeconds { get; set; } = 30;

    /// <summary>
    /// When true, an orphan partition is maintained for messages with no matching label partition
    /// or whose label partition has no active subscribers.
    /// When Acknowledge = WaitForAcknowledge the orphan must have at least one subscriber.
    /// </summary>
    public bool EnableOrphanPartition { get; set; } = true;
}
