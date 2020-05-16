using Newtonsoft.Json;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("Channel")]
        public string Channel { get; set; }

        /// <summary>
        /// Queue id
        /// </summary>
        [JsonPropertyName("Id")]
        public ushort Id { get; set; }

        /// <summary>
        /// Pending high priority messages in the queue
        /// </summary>
        [JsonPropertyName("PriorityMessages")]
        public int PriorityMessages { get; set; }

        /// <summary>
        /// Pending regular messages in the queue
        /// </summary>
        [JsonPropertyName("Messages")]
        public int Messages { get; set; }

        /// <summary>
        /// Queue current status
        /// </summary>
        [JsonPropertyName("Status")]
        public string Status { get; set; }

        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        [JsonPropertyName("OnlyFirstAcquirer")]
        public bool OnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        [JsonPropertyName("RequestAcknowledge")]
        public bool RequestAcknowledge { get; set; }

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        [JsonPropertyName("AcknowledgeTimeout")]
        public int AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        [JsonPropertyName("MessageTimeout")]
        public int MessageTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        [JsonPropertyName("UseMessageId")]
        public bool UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        [JsonPropertyName("WaitForAcknowledge")]
        public bool WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        [JsonPropertyName("HideClientNames")]
        public bool HideClientNames { get; set; }

        /// <summary>
        /// Total messages received from producers
        /// </summary>
        [JsonPropertyName("ReceivedMessages")]
        public long ReceivedMessages { get; set; }

        /// <summary>
        /// Total messages sent to consumers
        /// </summary>
        [JsonPropertyName("SentMessages")]
        public long SentMessages { get; set; }

        /// <summary>
        /// Total message send operation each message to each consumer
        /// </summary>
        [JsonPropertyName("Deliveries")]
        public long Deliveries { get; set; }

        /// <summary>
        /// Total unacknowledged messages
        /// </summary>
        [JsonPropertyName("NegativeAcks")]
        public long NegativeAcks { get; set; }

        /// <summary>
        /// Total acknowledged messages
        /// </summary>
        [JsonPropertyName("Acks")]
        public long Acks { get; set; }

        /// <summary>
        /// Total timed out messages
        /// </summary>
        [JsonPropertyName("TimeoutMessages")]
        public long TimeoutMessages { get; set; }

        /// <summary>
        /// Total saved messages
        /// </summary>
        [JsonPropertyName("SavedMessages")]
        public long SavedMessages { get; set; }

        /// <summary>
        /// Total removed messages
        /// </summary>
        [JsonPropertyName("RemovedMessages")]
        public long RemovedMessages { get; set; }

        /// <summary>
        /// Total error count
        /// </summary>
        [JsonPropertyName("Errors")]
        public long Errors { get; set; }

        /// <summary>
        /// Last message receive date in UNIX milliseconds
        /// </summary>
        [JsonPropertyName("LastMessageReceived")]
        public long LastMessageReceived { get; set; }

        /// <summary>
        /// Last message send date in UNIX milliseconds
        /// </summary>
        [JsonPropertyName("LastMessageSent")]
        public long LastMessageSent { get; set; }

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("MessageLimit")]
        public int MessageLimit { get; set; }

        /// <summary>
        /// Maximum message size limit
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("MessageSizeLimit")]
        public ulong MessageSizeLimit { get; set; }
    }
}