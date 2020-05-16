using Newtonsoft.Json;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Active queues in the channel
        /// </summary>
        [JsonPropertyName("Queues")]
        public ushort[] Queues { get; set; }

        /// <summary>
        /// If true, channel can have multiple queues with multiple content type.
        /// If false, each channel can only have one queue
        /// </summary>
        [JsonPropertyName("AllowMultipleQueues")]
        public bool AllowMultipleQueues { get; set; } = true;

        /// <summary>
        /// Allowed queue id list for the channel
        /// </summary>
        [JsonPropertyName("AllowedQueues")]
        public ushort[] AllowedQueues { get; set; }

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
        /// Online and subscribed clients in the channel
        /// </summary>
        [JsonPropertyName("ActiveClients")]
        public int ActiveClients { get; set; }

        /// <summary>
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("ClientLimit")]
        public int ClientLimit { get; set; }

        /// <summary>
        /// Maximum queue limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("QueueLimit")]
        public int QueueLimit { get; set; }

        /// <summary>
        /// If true, channel will be destroyed when there are no messages in queues and there are no consumers available
        /// </summary>
        [JsonPropertyName("DestroyWhenEmpty")]
        public bool DestroyWhenEmpty { get; set; }

    }
}