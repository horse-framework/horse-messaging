using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Twino.Protocols.TMQ.Models
{
    /// <summary>
    /// Channel information
    /// </summary>
    public class ChannelInformation
    {
        /// <summary>
        /// Channel name
        /// </summary>
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Active queues in the channel
        /// </summary>
        [JsonProperty("queues")]
        [JsonPropertyName("queues")]
        public ushort[] Queues { get; set; }

        /// <summary>
        /// If true, channel can have multiple queues with multiple content type.
        /// If false, each channel can only have one queue
        /// </summary>
        [JsonProperty("allowMultipleQueues")]
        [JsonPropertyName("allowMultipleQueues")]
        public bool AllowMultipleQueues { get; set; } = true;

        /// <summary>
        /// Allowed queue id list for the channel
        /// </summary>
        [JsonProperty("allowedQueues")]
        [JsonPropertyName("allowedQueues")]
        public ushort[] AllowedQueues { get; set; }

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
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("clientLimit")]
        [JsonPropertyName("clientLimit")]
        public int ClientLimit { get; set; }
        
        /// <summary>
        /// Maximum queue limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("queueLimit")]
        [JsonPropertyName("queueLimit")]
        public int QueueLimit { get; set; }

        /// <summary>
        /// If true, channel will be destroyed when there are no messages in queues and there are no consumers available
        /// </summary>
        [JsonProperty("destroyWhenEmpty")]
        [JsonPropertyName("destroyWhenEmpty")]
        public bool DestroyWhenEmpty { get; set; }

    }
}