using System;
using System.Text.Json.Serialization;

namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class QueueOptions
    {
        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        [JsonPropertyName("SendOnlyFirstAcquirer")]
        public bool? SendOnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        [JsonPropertyName("RequestAcknowledge")]
        public bool? RequestAcknowledge { get; set; }

        /// <summary>
        /// Used for serializing timespan as integer value in milliseconds
        /// </summary>
        [JsonPropertyName("AcknowledgeTimeout")]
        public int? AcknowledgeTimeout { get; set; }

        /// <summary>
        /// Used for serializing timespan as integer value in milliseconds
        /// </summary>
        [JsonPropertyName("MessageTimeout")]
        public int? MessageTimeout { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        [JsonPropertyName("UseMessageId")]
        public bool? UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        [JsonPropertyName("WaitForAcknowledge")]
        public bool? WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        [JsonPropertyName("HideClientNames")]
        public bool? HideClientNames { get; set; }

        /// <summary>
        /// Default status for the queue
        /// </summary>
        [JsonPropertyName("Status")]
        public MessagingQueueStatus? Status { get; set; }

        /// <summary>
        /// Registry key for message delivery handler
        /// </summary>
        [JsonPropertyName("MessageDeliveryHandler")]
        public string MessageDeliveryHandler { get; set; }

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("MessageLimit")]
        public int? MessageLimit { get; set; }
    }
}