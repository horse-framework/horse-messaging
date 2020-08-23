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
        [JsonProperty("Name")]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Active queues in the channel
        /// </summary>
        [JsonProperty("Queues")]
        [JsonPropertyName("Queues")]
        public ushort[] Queues { get; set; }

        /// <summary>
        /// If true, channel can have multiple queues with multiple content type.
        /// If false, each channel can only have one queue
        /// </summary>
        [JsonProperty("AllowMultipleQueues")]
        [JsonPropertyName("AllowMultipleQueues")]
        public bool AllowMultipleQueues { get; set; } = true;

        /// <summary>
        /// Allowed queue id list for the channel
        /// </summary>
        [JsonProperty("AllowedQueues")]
        [JsonPropertyName("AllowedQueues")]
        public ushort[] AllowedQueues { get; set; }

        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        [JsonProperty("OnlyFirstAcquirer")]
        [JsonPropertyName("OnlyFirstAcquirer")]
        public bool OnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        [JsonProperty("RequestAcknowledge")]
        [JsonPropertyName("RequestAcknowledge")]
        public bool RequestAcknowledge { get; set; }

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
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        [JsonProperty("WaitForAcknowledge")]
        [JsonPropertyName("WaitForAcknowledge")]
        public bool WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        [JsonProperty("HideClientNames")]
        [JsonPropertyName("HideClientNames")]
        public bool HideClientNames { get; set; }

        /// <summary>
        /// Online and subscribed clients in the channel
        /// </summary>
        [JsonProperty("ActiveClients")]
        [JsonPropertyName("ActiveClients")]
        public int ActiveClients { get; set; }

        /// <summary>
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("ClientLimit")]
        [JsonPropertyName("ClientLimit")]
        public int ClientLimit { get; set; }

        /// <summary>
        /// Maximum queue limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonProperty("QueueLimit")]
        [JsonPropertyName("QueueLimit")]
        public int QueueLimit { get; set; }

        /// <summary>
        /// If true, channel will be destroyed when there are no messages in queues and there are no consumers available
        /// </summary>
        [JsonProperty("DestroyWhenEmpty")]
        [JsonPropertyName("DestroyWhenEmpty")]
        public bool DestroyWhenEmpty { get; set; }

    }
}