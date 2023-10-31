using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace AdvancedSample.Server.Models
{
    public class QueueConfig
    {
        public InMemoryQueueConfig InMemory { get; set; }
        public PersistentQueueConfig Persistent { get; set; }
    }

    public class InMemoryQueueConfig
    {
        public bool Enabled { get; set; }
        public QueueConfigOptions Options { get; set; }
    }

    public class QueueConfigOptions
    {
        public QueueAckDecision Acknowledge { get; set; } = QueueAckDecision.WaitForAcknowledge;
        public CommitWhen CommitWhen { get; set; } = CommitWhen.AfterReceived;
        public string AcknowledgeTimeout { get; set; } = "1s";
        public string MessageTimeout { get; set; } = "1s";
        public int MessageLimit { get; set; } = 0; // unlimited
        public int ClientLimit { get; set; } = 0; // unlimited
        public int DelayBetweenMessages { get; set; } = 0; // in miliseconds
        public MessageLimitExceededStrategy LimitExceededStrategy { get; set; } = MessageLimitExceededStrategy.RejectNewMessage;
        public ulong MessageSizeLimit { get; set; } = 0;  // unlimited
        public int PutBackDelay { get; set; } = 0;  // unlimited
        public PutBackDecision PutBackDecision { get; set; } = PutBackDecision.Regular;
        public QueueDestroy QueueAutoDestroy { get; set; } = QueueDestroy.Disabled;
        public bool AutoQueueCreation { get; set; } = true;
    }

    public class PersistentQueueConfig
    {
        public bool Enabled { get; set; }
        public QueueConfigOptions Options { get; set; }
        public PersistentQueueDataConfig DataConfig { get; set; }
       

    }

    public class PersistentQueueDataConfig
    {
        public string ConfigRelativePath { get; set; } = "data";
        public string DefaultRelativeDataPath { get; set; } = "data/config.json";
        public string AutoFlushInterval { get; set; } = "1s";
        public bool UseSeperateFolder { get; set; } = false;
        public bool KeepLastBackup { get; set; } = true;
        public PersistentQueueAutoShrinkConfig AutoShrink { get; set; }
    }

    public class PersistentQueueAutoShrinkConfig
    {
        public bool Enabled { get; set; } = false;
        public string Interval { get; set; } = "1s";
    }
   
}
