using System;
using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
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

    /// <summary>
    /// Null in infos sent by older nodes, in which case the replica keeps its current value
    /// </summary>
    public string CommitWhen { get; set; }
        
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

    /// <summary>
    /// Applies the behavioural options of the Main node's queue to a replica's queue options.
    /// Creating and updating a replica both use this one mapping. They used to map different
    /// subsets, so a promoted replica could commit, retry, check uniqueness and deliver differently.
    /// Enum values left null by older nodes keep the current value.
    /// </summary>
    internal void ApplyTo(QueueOptions options)
    {
        if (!string.IsNullOrEmpty(QueueType))
            options.Type = Enums.Parse<QueueType>(QueueType, true, EnumFormat.Description);

        if (!string.IsNullOrEmpty(Acknowledge))
            options.Acknowledge = Enums.Parse<QueueAckDecision>(Acknowledge, true, EnumFormat.Description);

        if (!string.IsNullOrEmpty(CommitWhen))
            options.CommitWhen = Enums.Parse<CommitWhen>(CommitWhen, true, EnumFormat.Description);

        if (!string.IsNullOrEmpty(PutBack))
            options.PutBack = Enums.Parse<PutBackDecision>(PutBack, true, EnumFormat.Description);

        if (!string.IsNullOrEmpty(AutoDestroy))
            options.AutoDestroy = Enums.Parse<QueueDestroy>(AutoDestroy, true, EnumFormat.Description);

        if (!string.IsNullOrEmpty(LimitExceededStrategy))
            options.LimitExceededStrategy = Enums.Parse<MessageLimitExceededStrategy>(LimitExceededStrategy, true, EnumFormat.Description);

        if (MessageTimeout != null)
            options.MessageTimeout = new MessageTimeoutStrategy
            {
                MessageDuration = MessageTimeout.MessageDuration,
                Policy = Enums.Parse<MessageTimeoutPolicy>(MessageTimeout.Policy, true, EnumFormat.Description),
                TargetName = MessageTimeout.TargetName
            };

        options.AcknowledgeTimeout = TimeSpan.FromMilliseconds(AcknowledgeTimeout);
        options.PutBackDelay = PutBackDelay;
        options.DelayBetweenMessages = DelayBetweenMessages;
        options.ClientLimit = ClientLimit;
        options.MessageLimit = MessageLimit;
        options.MessageSizeLimit = MessageSizeLimit;
        options.MessageIdUniqueCheck = MessageIdUniqueCheck;

        // Without this a sub-queue copy got a PartitionManager of its own and a parent copy kept
        // the server default partition settings.
        if (IsPartitionQueue)
            options.Partition = null;
        else if (Partition != null)
        {
            // Copied into the existing object: a PartitionManager already reads it
            PartitionOptions target = options.Partition ??= new PartitionOptions();
            target.Enabled = Partition.Enabled;
            target.MaxPartitionCount = Partition.MaxPartitionCount;
            target.SubscribersPerPartition = Partition.SubscribersPerPartition;
            target.AutoDestroy = Partition.AutoDestroy;
            target.AutoDestroyIdleSeconds = Partition.AutoDestroyIdleSeconds;
            target.AutoAssignWorkers = Partition.AutoAssignWorkers;
            target.MaxPartitionsPerWorker = Partition.MaxPartitionsPerWorker;
        }
    }

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
