using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace HorseService;

public class OptionsBuilder
{
    private readonly AppOptions _options = new AppOptions();

    private OptionsBuilder()
    {
    }

    public static OptionsBuilder Create()
    {
        return new OptionsBuilder();
    }

    public OptionsBuilder LoadFromEnvironment()
    {
        string datapath = Environment.GetEnvironmentVariable("HORSE_DATA_PATH");

        if (!string.IsNullOrEmpty(datapath))
            _options.DataPath = datapath;

        if (!Directory.Exists(_options.DataPath))
            Directory.CreateDirectory(_options.DataPath);

        LoadJockeyFromEnvironment();
        LoadClusterFromEnvironment();
        LoadLimitsFromEnvironment();
        LoadChannelFromEnvironment();
        LoadCacheFromEnvironment();
        LoadQueueFromEnvironment();

        return this;
    }

    private void LoadJockeyFromEnvironment()
    {
        _options.JockeyEnabled = bool.Parse(Environment.GetEnvironmentVariable("HORSE_JOCKEY") ?? "true");
        _options.JockeyUsername = Environment.GetEnvironmentVariable("HORSE_JOCKEY_USERNAME") ?? "";
        _options.JockeyPassword = Environment.GetEnvironmentVariable("HORSE_JOCKEY_PASSWORD") ?? "";
    }

    private void LoadClusterFromEnvironment()
    {
        int instanceCount = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_HOST_COUNT") ?? "1");

        if (instanceCount == 1)
            return;

        string hostTemplate = Environment.GetEnvironmentVariable("HORSE_HOSTNAME") ?? "localhost";
        int instanceIndexStart = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_HOST_START_INDEX") ?? "0");
        string hostname = Environment.GetEnvironmentVariable("HOSTNAME");

        _options.NodeName = hostname;
        _options.Host = hostname;
        _options.ClusterSecret = Environment.GetEnvironmentVariable("HORSE_CLUSTER_SECRET") ?? "no-secret";

        int index = instanceIndexStart;
        for (int i = 0; i < instanceCount; i++)
        {
            string replicaHost = hostTemplate.Replace("INDEX", index.ToString());

            index++;
            if (replicaHost.Equals(hostname, StringComparison.InvariantCultureIgnoreCase))
                continue;

            _options.OtherNodes.Add(new NodeOptions {Name = replicaHost, Host = replicaHost});
        }

        _options.ClusterMode = Enums.Parse<ClusterMode>(Environment.GetEnvironmentVariable("HORSE_CLUSTER_TYPE") ?? "Reliable", true, EnumFormat.Name);
        _options.ReplicaAcknowledge = Enums.Parse<ReplicaAcknowledge>(Environment.GetEnvironmentVariable("HORSE_REPLICA_ACK") ?? "OnlySuccessor", true, EnumFormat.Name);
    }

    private void LoadLimitsFromEnvironment()
    {
        _options.ChannelLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_CHANNEL_LIMIT") ?? "0");
        _options.ClientLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_CLIENT_LIMIT") ?? "0");
        _options.QueueLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_LIMIT") ?? "0");
        _options.RouterLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_ROUTER_LIMIT") ?? "0");
    }

    private void LoadChannelFromEnvironment()
    {
        _options.ChannelAutoCreate = bool.Parse(Environment.GetEnvironmentVariable("HORSE_CHANNEL_AUTO_DESTROY") ?? "true");
        _options.ChannelAutoDestroy = bool.Parse(Environment.GetEnvironmentVariable("HORSE_CHANNEL_AUTO_CREATE") ?? "true");
        _options.ChannelSendLatestMessage = bool.Parse(Environment.GetEnvironmentVariable("HORSE_CHANNEL_SEND_LATEST_MESSAGE") ?? "true");
        _options.ChannelSubscriberLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_CHANNEL_SUBSCRIBER_LIMIT") ?? "0");
    }

    private void LoadCacheFromEnvironment()
    {
        _options.CacheMaxKeys = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_CACHE_MAX_KEYS") ?? "0");
        _options.CacheMaxValueSize = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_CACHE_MAX_VALUE_SIZE") ?? "0");
    }

    private void LoadQueueFromEnvironment()
    {
        _options.QueueAutoCreate = bool.Parse(Environment.GetEnvironmentVariable("HORSE_QUEUE_AUTO_CREATE") ?? "true");
        _options.QueueAck = Enums.Parse<QueueAckDecision>(Environment.GetEnvironmentVariable("HORSE_QUEUE_ACK") ?? "WaitForAcknowledge");
        _options.QueueDestroy = Enums.Parse<QueueDestroy>(Environment.GetEnvironmentVariable("HORSE_QUEUE_AUTO_DESTROY") ?? "Disabled");
        _options.QueueCommitWhen = Enums.Parse<CommitWhen>(Environment.GetEnvironmentVariable("HORSE_QUEUE_COMMIT_WHEN") ?? "AfterReceived");
        _options.QueuePutback = Enums.Parse<PutBackDecision>(Environment.GetEnvironmentVariable("HORSE_QUEUE_PUTBACK") ?? "Regular");
        _options.QueueMsgExceedStrategy = Enums.Parse<MessageLimitExceededStrategy>(Environment.GetEnvironmentVariable("HORSE_QUEUE_MSG_EXCEED_STRATEGY") ?? "RejectNewMessage", true, EnumFormat.Name);

        _options.QueueAckTimeout = TimeSpan.FromSeconds(Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_ACK_TIMEOUT") ?? "30"));
        _options.QueuePutbackDelay = TimeSpan.FromMilliseconds(Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_ACK_TIMEOUT") ?? "0"));

        _options.QueueConsumerLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_CONSUMER_LIMIT") ?? "0");
        _options.QueueMessageLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_MESSAGE_LIMIT") ?? "0");
        _options.QueueMessageSizeLimit = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_MESSAGE_SIZE_LIMIT") ?? "0");
        _options.QueueDelayBetweenMsgs = Convert.ToInt32(Environment.GetEnvironmentVariable("HORSE_QUEUE_DELAY_BETWEEN_MSGS") ?? "0");
        _options.QueueMessageIdUniqueCheck = bool.Parse(Environment.GetEnvironmentVariable("HORSE_QUEUE_MESSAGE_ID_UNIQUE_CHECK") ?? "false");

        string[] timeout = (Environment.GetEnvironmentVariable("HORSE_QUEUE_MESSAGE_TIMEOUT") ?? "0;NoTimeout;").Split(';');
        _options.QueueMessageTimeout = new MessageTimeoutStrategy
        {
            MessageDuration = Convert.ToInt32(timeout.Length > 0 ? timeout[0] : "0"),
            Policy = Enums.Parse<MessageTimeoutPolicy>(timeout.Length > 1 ? timeout[1] : "NoTimeout", true, EnumFormat.Name),
            TargetName = timeout.Length > 2 ? timeout[2] : string.Empty
        };

        _options.QueueUsePersistent = bool.Parse(Environment.GetEnvironmentVariable("HORSE_QUEUE_USE_PERSISTENT") ?? "true");
        _options.QueueUseMemory = bool.Parse(Environment.GetEnvironmentVariable("HORSE_QUEUE_USE_MEMORY") ?? "true");

        string defaultManager = Environment.GetEnvironmentVariable("HORSE_QUEUE_DEFAULT_MANAGER") ?? "Persistent";
        _options.QueueUsePersistentManagerAsDefault = defaultManager.Equals("Persistent", StringComparison.InvariantCultureIgnoreCase);
    }

    public AppOptions Build()
    {
        return _options;
    }
}