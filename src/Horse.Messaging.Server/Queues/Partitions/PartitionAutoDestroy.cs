namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Auto-destroy rules specific to partition queues.
/// Applies only to individual partitions; the parent queue and orphan partition are not affected.
/// </summary>
public enum PartitionAutoDestroy
{
    /// <summary>Partition is never auto-destroyed.</summary>
    Disabled = 0,

    /// <summary>Destroyed when the partition has no consumers (messages may still exist).</summary>
    NoConsumers = 1,

    /// <summary>Destroyed when the partition has no messages (consumers may still be subscribed).</summary>
    NoMessages = 2,

    /// <summary>Destroyed when the partition has neither messages nor consumers.</summary>
    Empty = 3
}

