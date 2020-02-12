using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.Protocols.TMQ.Models
{
    /// <summary>
    /// Queue information
    /// </summary>
    public class QueueInformation
    {
        /// <summary>
        /// Queue channel name
        /// </summary>
        [JsonProperty("channel")]
        [JsonPropertyName("channel")]
        public string Channel { get; set; }

        /// <summary>
        /// Queue id
        /// </summary>
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public ushort Id { get; set; }

        /// <summary>
        /// Pending high priority messages in the queue
        /// </summary>
        [JsonProperty("highPrioMsgs")]
        [JsonPropertyName("highPrioMsgs")]
        public int InQueueHighPriorityMessages { get; set; }

        /// <summary>
        /// Pending regular messages in the queue
        /// </summary>
        [JsonProperty("regularMsgs")]
        [JsonPropertyName("regularMsgs")]
        public int InQueueRegularMessages { get; set; }

        /// <summary>
        /// Queue current status
        /// </summary>
        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; }

        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        [JsonProperty("onlyFirstAcquirer")]
        [JsonPropertyName("onlyFirstAcquirer")]
        public bool OnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        [JsonProperty("requestAck")]
        [JsonPropertyName("requestAck")]
        public bool RequestAcknowledge { get; set; }

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        [JsonProperty("ackTimeout")]
        [JsonPropertyName("ackTimeout")]
        public int AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        [JsonProperty("msgTimeout")]
        [JsonPropertyName("msgTimeout")]
        public int MessageTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        [JsonProperty("useMsgId")]
        [JsonPropertyName("useMsgId")]
        public bool UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        [JsonProperty("waitForAck")]
        [JsonPropertyName("waitForAck")]
        public bool WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        [JsonProperty("hideClientNames")]
        [JsonPropertyName("hideClientNames")]
        public bool HideClientNames { get; set; }

        /// <summary>
        /// Total messages received from producers
        /// </summary>
        [JsonProperty("receivedMsgs")]
        [JsonPropertyName("receivedMsgs")]
        public long ReceivedMessages { get; set; }

        /// <summary>
        /// Total messages sent to consumers
        /// </summary>
        [JsonProperty("sentMsgs")]
        [JsonPropertyName("sentMsgs")]
        public long SentMessages { get; set; }

        /// <summary>
        /// Total message send operation each message to each consumer
        /// </summary>
        [JsonProperty("deliveries")]
        [JsonPropertyName("deliveries")]
        public long Deliveries { get; set; }

        /// <summary>
        /// Total unacknowledged messages
        /// </summary>
        [JsonProperty("unacks")]
        [JsonPropertyName("unacks")]
        public long Unacknowledges { get; set; }

        /// <summary>
        /// Total acknowledged messages
        /// </summary>
        [JsonProperty("acks")]
        [JsonPropertyName("acks")]
        public long Acknowledges { get; set; }

        /// <summary>
        /// Total timed out messages
        /// </summary>
        [JsonProperty("timeoutMsgs")]
        [JsonPropertyName("timeoutMsgs")]
        public long TimeoutMessages { get; set; }

        /// <summary>
        /// Total saved messages
        /// </summary>
        [JsonProperty("savedMsgs")]
        [JsonPropertyName("savedMsgs")]
        public long SavedMessages { get; set; }

        /// <summary>
        /// Total removed messages
        /// </summary>
        [JsonProperty("removedMsgs")]
        [JsonPropertyName("removedMsgs")]
        public long RemovedMessages { get; set; }

        /// <summary>
        /// Total error count
        /// </summary>
        [JsonProperty("errors")]
        [JsonPropertyName("errors")]
        public long Errors { get; set; }

        /// <summary>
        /// Last message receive date in UNIX milliseconds
        /// </summary>
        [JsonProperty("lastMsgReceived")]
        [JsonPropertyName("lastMsgReceived")]
        public long LastMessageReceived { get; set; }

        /// <summary>
        /// Last message send date in UNIX milliseconds
        /// </summary>
        [JsonProperty("lastMsgSent")]
        [JsonPropertyName("lastMsgSent")]
        public long LastMessageSent { get; set; }

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("messageLimit")]
        [JsonPropertyName("messageLimit")]
        public int MessageLimit { get; set; }
        
        /// <summary>
        /// Maximum message size limit
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("messageSize")]
        [JsonPropertyName("messageSize")]
        public ulong MessageSizeLimit { get; set; }
    }
}