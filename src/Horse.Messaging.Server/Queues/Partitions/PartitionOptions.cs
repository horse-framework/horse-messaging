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
    /// Maximum number of partitions. 0 = unlimited.
    /// </summary>
    public int MaxPartitionCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of subscribers per partition (maps to ClientLimit on each partition queue).
    /// </summary>
    public int SubscribersPerPartition { get; set; } = 1;

    /// <summary>
    /// Auto-destroy rule evaluated for each individual partition.
    /// Does NOT affect the parent queue.
    /// </summary>
    public PartitionAutoDestroy AutoDestroy { get; set; } = PartitionAutoDestroy.Disabled;

    /// <summary>
    /// Idle evaluation interval in seconds for the auto-destroy check.
    /// </summary>
    public int AutoDestroyIdleSeconds { get; set; } = 30;

    /// <summary>
    /// When true, label-less subscribers are placed in a worker pool and automatically
    /// assigned to newly created labeled partitions on demand.
    /// This enables dynamic tenant/label scenarios where workers don't know their labels upfront.
    /// </summary>
    public bool AutoAssignWorkers { get; set; } = false;

    /// <summary>
    /// Maximum number of partitions a single worker can be assigned to simultaneously
    /// when <see cref="AutoAssignWorkers"/> is true.
    /// 0 = unlimited (a worker can serve any number of partitions).
    /// Default is 1 (one worker per one partition at a time).
    /// <para>
    /// Each partition has its own message processing loop, so a worker assigned to
    /// multiple partitions processes messages from each partition independently and concurrently.
    /// With <c>waitAcknowledge</c>, per-partition FIFO ordering is still guaranteed.
    /// </para>
    /// </summary>
    public int MaxPartitionsPerWorker { get; set; } = 1;
}
