using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace MessagingServer;

public class NodeOptions
{
    public string Name { get; set; }
    public string Host { get; set; }
}

public class AppOptions
{
    public string DataPath { get; set; } = "/etc/horse/data";
    public int Port { get; set; } = 2626;

    public int JockeyPort { get; set; } = 2627;
    public bool JockeyEnabled { get; set; }
    public string JockeyUsername { get; set; } = "";
    public string JockeyPassword { get; set; } = "";


    public ClusterMode ClusterMode { get; set; }
    public ReplicaAcknowledge ReplicaAcknowledge { get; set; }
    public string ClusterSecret { get; set; }
    public string NodeName { get; set; }
    public string Host { get; set; }
    public List<NodeOptions> OtherNodes { get; } = new List<NodeOptions>();

    public int ChannelLimit { get; set; }
    public int ClientLimit { get; set; }
    public int QueueLimit { get; set; }
    public int RouterLimit { get; set; }

    public bool ChannelAutoCreate { get; set; } = true;
    public bool ChannelAutoDestroy { get; set; } = true;
    public bool ChannelSendLatestMessage { get; set; } = true;
    public int ChannelSubscriberLimit { get; set; }

    public int CacheMaxKeys { get; set; }
    public int CacheMaxValueSize { get; set; }

    public bool QueueAutoCreate { get; set; } = true;
    public QueueType QueueType { get; set; }
    public QueueAckDecision QueueAck { get; set; }
    public TimeSpan QueueAckTimeout { get; set; }
    public QueueDestroy QueueDestroy { get; set; }
    public CommitWhen QueueCommitWhen { get; set; }
    public PutBackDecision QueuePutback { get; set; }
    public TimeSpan QueuePutbackDelay { get; set; }
    public int QueueConsumerLimit { get; set; }
    public int QueueMessageLimit { get; set; }
    public MessageLimitExceededStrategy QueueMsgExceedStrategy { get; set; }
    public int QueueMessageSizeLimit { get; set; }
    public MessageTimeoutStrategy QueueMessageTimeout { get; set; }
    public int QueueDelayBetweenMsgs { get; set; }
    public bool QueueMessageIdUniqueCheck { get; set; }
    public bool QueueUsePersistent { get; set; }
    public bool QueueUseMemory { get; set; }
    public bool QueueUsePersistentManagerAsDefault { get; set; }
    public bool OverWebSocket { get; set; }
    public bool UseElasticLogs { get; set; } = true;
}