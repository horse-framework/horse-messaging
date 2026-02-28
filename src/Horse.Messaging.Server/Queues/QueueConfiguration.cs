using System;
using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Partitions;

namespace Horse.Messaging.Server.Queues;

/// <summary>
/// Persistent partition configuration data stored inside QueueConfiguration.
/// </summary>
public class PartitionConfigurationData
{
    public bool   Enabled                 { get; set; }
    public int    MaxPartitionCount       { get; set; }
    public int    SubscribersPerPartition { get; set; }
    public string AutoDestroy             { get; set; }
    public int    AutoDestroyIdleSeconds  { get; set; }
    public bool   EnableOrphanPartition   { get; set; }
}

/// <summary>
/// Sub-queue partition metadata stored inside QueueConfiguration.
/// Persisted when IsPartitionQueue = true so the queue can be re-attached to its
/// parent's PartitionManager on restart.
/// </summary>
public class SubPartitionData
{
    /// <summary>Parent queue name.</summary>
    public string ParentQueueName { get; set; }

    /// <summary>Partition id (base-62 or "Orphan").</summary>
    public string PartitionId { get; set; }

    /// <summary>Worker routing label. Null for label-less or orphan partitions.</summary>
    public string Label { get; set; }

    /// <summary>True when this is the orphan (fallback) partition.</summary>
    public bool IsOrphan { get; set; }
}

/// <summary>
/// Persistent queue configuration data
/// </summary>
public class QueueConfiguration
{
    /// <summary>
    /// Queue name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Queue topic
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Queue Manager Name
    /// </summary>
    public string ManagerName { get; set; }

    /// <summary>
    /// Queue status
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Payload value is used by queue manager.
    /// Each queue manager can keep it's persistent configurations in that property
    /// </summary>
    public string ManagerPayload { get; set; }

    /// <summary>
    /// Acknowledge decision. Default is just request.
    /// </summary>
    public string Acknowledge { get; set; }

    /// <summary>
    /// Option for when the commit message is sent to producer
    /// </summary>
    public string CommitWhen { get; set; }

    /// <summary>
    /// When acknowledge is required, maximum duration for waiting acknowledge message (in milliseconds)
    /// </summary>
    public long AcknowledgeTimeout { get; set; }

    /// <summary>
    /// When message queuing is active, maximum time for a message wait (in milliseconds)
    /// </summary>
    public MessageTimeoutStrategyInfo MessageTimeout { get; set; }

    /// <summary>
    /// Default type for the queue
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Maximum message limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int MessageLimit { get; set; }

    /// <summary>
    /// Decision for when message limit is exceeded and a new message is received.
    /// Default value is RejectNewMessage.
    /// </summary>
    public string LimitExceededStrategy { get; set; }

    /// <summary>
    /// Maximum message size limit
    /// Zero is unlimited
    /// </summary>
    public ulong MessageSizeLimit { get; set; }

    /// <summary>
    /// Maximum client limit of the queue
    /// Zero is unlimited
    /// </summary>
    public int ClientLimit { get; set; }

    /// <summary>
    /// Waits in milliseconds after sending each message to it's consumers.
    /// Zero is no delay.
    /// </summary>
    public int DelayBetweenMessages { get; set; }

    /// <summary>
    /// Queue put back decision option
    /// </summary>
    public string PutBack { get; set; }

    /// <summary>
    /// Waits in milliseconds before putting message back into the queue.
    /// Zero is no delay.
    /// </summary>
    public int PutBackDelay { get; set; }

    /// <summary>
    /// Queue auto destroy options. Default value is NoMessagesAndConsumers.
    /// </summary>
    public string AutoDestroy { get; set; }

    /// <summary>
    /// If true, server checks all message id values and reject new messages with same id.
    /// Enabling that feature has performance penalty about 0.03 ms for each message. 
    /// </summary>
    public bool MessageIdUniqueCheck { get; set; }

    /// <summary>
    /// Serialized partition options. Null when partitioning is disabled.
    /// </summary>
    public PartitionConfigurationData Partition { get; set; }

    /// <summary>
    /// Non-null when this queue is a partition sub-queue (IsPartitionQueue = true).
    /// Carries the parent queue name, partition id, label and orphan flag so that
    /// the PartitionManager can re-attach this queue on server restart.
    /// </summary>
    public SubPartitionData SubPartition { get; set; }

    /// <summary>
    /// Creates new queue configuration from queue
    /// </summary>
    public static QueueConfiguration Create(HorseQueue queue)
    {
        SubPartitionData subPartData = null;
        if (queue.IsPartitionQueue && queue.PartitionMeta != null)
        {
            subPartData = new SubPartitionData
            {
                ParentQueueName = queue.PartitionMeta.ParentQueueName,
                PartitionId     = queue.PartitionMeta.PartitionId,
                Label           = queue.PartitionMeta.Label,
                IsOrphan        = queue.PartitionMeta.IsOrphan
            };
        }

        return new QueueConfiguration
        {
            Name = queue.Name,
            Topic = queue.Topic,
            ManagerName = queue.ManagerName,
            Status = queue.Status.AsString(EnumFormat.Description),
            ManagerPayload = queue.ManagerPayload,
            Acknowledge = queue.Options.Acknowledge.AsString(EnumFormat.Description),
            Type = queue.Options.Type.AsString(EnumFormat.Description),
            AutoDestroy = queue.Options.AutoDestroy.AsString(EnumFormat.Description),
            CommitWhen = queue.Options.CommitWhen.AsString(EnumFormat.Description),
            PutBack = queue.Options.PutBack.AsString(EnumFormat.Description),
            LimitExceededStrategy = queue.Options.LimitExceededStrategy.AsString(EnumFormat.Description),
            AcknowledgeTimeout = Convert.ToInt64(queue.Options.AcknowledgeTimeout.TotalMilliseconds),
            ClientLimit = queue.Options.ClientLimit,
            MessageLimit = queue.Options.MessageLimit,
            DelayBetweenMessages = queue.Options.DelayBetweenMessages,
            PutBackDelay = queue.Options.PutBackDelay,
            MessageSizeLimit = queue.Options.MessageSizeLimit,
            MessageTimeout = new MessageTimeoutStrategyInfo(queue.Options.MessageTimeout.MessageDuration, queue.Options.MessageTimeout.Policy.AsString(EnumFormat.Description), queue.Options.MessageTimeout.TargetName),
            MessageIdUniqueCheck = queue.Options.MessageIdUniqueCheck,
            Partition = queue.Options.Partition == null ? null : new PartitionConfigurationData
            {
                Enabled                = queue.Options.Partition.Enabled,
                MaxPartitionCount      = queue.Options.Partition.MaxPartitionCount,
                SubscribersPerPartition = queue.Options.Partition.SubscribersPerPartition,
                AutoDestroy            = queue.Options.Partition.AutoDestroy.ToString(),
                AutoDestroyIdleSeconds = queue.Options.Partition.AutoDestroyIdleSeconds,
                EnableOrphanPartition  = queue.Options.Partition.EnableOrphanPartition
            },
            SubPartition = subPartData
        };
    }
}