using System.Text.Json.Serialization;

namespace Horse.Messaging.Client.Models
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class QueueOptions
    {
        /// <summary>
        /// Acknowledge decisions:
        /// "none": Acknowledge is disabled
        /// "request": Requests acknowledge but doesn't wait until ack is received
        /// "wait": Requests ack and waits until acknowledge is received
        /// </summary>
        [JsonPropertyName("Acknowledge")]
        public string Acknowledge { get; set; }

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

        /// <summary>
        /// Maximum client limit of the queue.
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("ClientLimit")]
        public int? ClientLimit { get; set; }

        /// <summary>
        /// Auto destroy options for the queue:
        /// "disabled": Auto destroy is disabled
        /// "no-message": Queue is destroyed when there is no message (even there are consumers)
        /// "no-consumer": Queue is destroyed when there is no consumer (even there are messages)
        /// "empty": Queue is destroyed when there is no message and no consumer
        /// </summary>
        [JsonPropertyName("AutoDestroy")]
        public string AutoDestroy { get; set; }

        /// <summary>
        /// Delay between messages in milliseconds.
        /// Useful when wait for acknowledge is disabled but you need to prevent overheat on consumers if producer pushes too many messages in a short duration.
        /// Zero is no delay.
        /// </summary>
        [JsonPropertyName("DelayBetweenMessages")]
        public int? DelayBetweenMessages { get; set; }

        /// <summary>
        /// Waits in milliseconds before putting message back into the queue.
        /// Zero is no delay.
        /// </summary>
        [JsonPropertyName("PutBackDelay")]
        public int? PutBackDelay { get; set; }
    }
}