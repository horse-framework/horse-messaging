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
        /// Queue name
        /// </summary>
        [JsonProperty("Name")]
        [JsonPropertyName("Nmae")]
        public string Name { get; set; }

        /// <summary>
        /// Queue topic
        /// </summary>
        [JsonProperty("Topic")]
        [JsonPropertyName("Topic")]
        public string Topic { get; set; }

        /// <summary>
        /// Pending high priority messages in the queue
        /// </summary>
        [JsonProperty("PriorityMessages")]
        [JsonPropertyName("PriorityMessages")]
        public int PriorityMessages { get; set; }

        /// <summary>
        /// Pending regular messages in the queue
        /// </summary>
        [JsonProperty("Messages")]
        [JsonPropertyName("Messages")]
        public int Messages { get; set; }

        /// <summary>
        /// Queue current status
        /// </summary>
        [JsonProperty("Status")]
        [JsonPropertyName("Status")]
        public string Status { get; set; }

        /// <summary>
        /// Queue acknowledge type
        /// </summary>
        [JsonProperty("Acknowledge")]
        [JsonPropertyName("Acknowledge")]
        public string Acknowledge { get; set; }

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        [JsonProperty("AcknowledgeTimeout")]
        [JsonPropertyName("AcknowledgeTimeout")]
        public int AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        [JsonProperty("MessageTimeout")]
        [JsonPropertyName("MessageTimeout")]
        public int MessageTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        [JsonProperty("UseMessageId")]
        [JsonPropertyName("UseMessageId")]
        public bool UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        [JsonProperty("HideClientNames")]
        [JsonPropertyName("HideClientNames")]
        public bool HideClientNames { get; set; }

        /// <summary>
        /// Total messages received from producers
        /// </summary>
        [JsonProperty("ReceivedMessages")]
        [JsonPropertyName("ReceivedMessages")]
        public long ReceivedMessages { get; set; }

        /// <summary>
        /// Total messages sent to consumers
        /// </summary>
        [JsonProperty("SentMessages")]
        [JsonPropertyName("SentMessages")]
        public long SentMessages { get; set; }

        /// <summary>
        /// Total message send operation each message to each consumer
        /// </summary>
        [JsonProperty("Deliveries")]
        [JsonPropertyName("Deliveries")]
        public long Deliveries { get; set; }

        /// <summary>
        /// Total unacknowledged messages
        /// </summary>
        [JsonProperty("NegativeAcks")]
        [JsonPropertyName("NegativeAcks")]
        public long NegativeAcks { get; set; }

        /// <summary>
        /// Total acknowledged messages
        /// </summary>
        [JsonProperty("Acks")]
        [JsonPropertyName("Acks")]
        public long Acks { get; set; }

        /// <summary>
        /// Total timed out messages
        /// </summary>
        [JsonProperty("TimeoutMessages")]
        [JsonPropertyName("TimeoutMessages")]
        public long TimeoutMessages { get; set; }

        /// <summary>
        /// Total saved messages
        /// </summary>
        [JsonProperty("SavedMessages")]
        [JsonPropertyName("SavedMessages")]
        public long SavedMessages { get; set; }

        /// <summary>
        /// Total removed messages
        /// </summary>
        [JsonProperty("RemovedMessages")]
        [JsonPropertyName("RemovedMessages")]
        public long RemovedMessages { get; set; }

        /// <summary>
        /// Total error count
        /// </summary>
        [JsonProperty("Errors")]
        [JsonPropertyName("Errors")]
        public long Errors { get; set; }

        /// <summary>
        /// Last message receive date in UNIX milliseconds
        /// </summary>
        [JsonProperty("LastMessageReceived")]
        [JsonPropertyName("LastMessageReceived")]
        public long LastMessageReceived { get; set; }

        /// <summary>
        /// Last message send date in UNIX milliseconds
        /// </summary>
        [JsonProperty("LastMessageSent")]
        [JsonPropertyName("LastMessageSent")]
        public long LastMessageSent { get; set; }

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("MessageLimit")]
        [JsonPropertyName("MessageLimit")]
        public int MessageLimit { get; set; }

        /// <summary>
        /// Maximum message size limit
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("MessageSizeLimit")]
        [JsonPropertyName("MessageSizeLimit")]
        public ulong MessageSizeLimit { get; set; }
    }
}