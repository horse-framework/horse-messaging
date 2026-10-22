using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Partitions;

namespace Horse.Messaging.Server.Cluster;

internal class NodeQueueHandlerHeader
{
    public string Key { get; set; }
    public string Value { get; set; }
}

internal class NodeQueueInfo
{
    public string Name { get; set; }
    public string HandlerName { get; set; }

    public string QueueType { get; set; }

    public bool Initialized { get; set; }

    public NodeQueueHandlerHeader[] Headers { get; internal set; }

    public string Topic { get; set; }
    public string Acknowledge { get; set; }
    public int AcknowledgeTimeout { get; set; }
    public string AutoDestroy { get; set; }

    public int ClientLimit { get; set; }
    public int MessageLimit { get; set; }
    public string LimitExceededStrategy { get; set; }
    public MessageTimeoutStrategyInfo MessageTimeout { get; set; }
    public int DelayBetweenMessages { get; set; }
    public ulong MessageSizeLimit { get; set; }
    public string PutBack { get; set; }
    public int PutBackDelay { get; set; }
        
    public bool MessageIdUniqueCheck { get; set; }

    /// <summary>
    /// Partition options of a partitioned parent queue. Null for partition sub-queues and for
    /// infos sent by older nodes, in which case the replica keeps its server default.
    /// </summary>
    public PartitionOptions Partition { get; set; }

    /// <summary>True when the queue is a partition sub-queue of <see cref="PartitionParent"/>.</summary>
    public bool IsPartitionQueue { get; set; }

    public string PartitionParent { get; set; }
    public string PartitionId { get; set; }
    public string PartitionLabel { get; set; }

    internal SubPartitionMeta CreatePartitionMeta()
    {
        if (!IsPartitionQueue)
            return null;

        return new SubPartitionMeta
        {
            ParentQueueName = PartitionParent,
            PartitionId = PartitionId,
            Label = PartitionLabel
        };
    }
}
