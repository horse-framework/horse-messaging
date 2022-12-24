using System.Text.Json.Serialization;

namespace Horse.Messaging.Client.Queues
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
        /// Default type for the queue
        /// </summary>
        [JsonPropertyName("Type")] 
        public MessagingQueueType? Type { get; set; }

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
        
        /// <summary>
        /// If true, server checks all message id values and reject new messages with same id.
        /// Enabling that feature has performance penalty about 0.03 ms for each message. 
        /// </summary>
        [JsonPropertyName("MessageIdUniqueCheck")]
        public bool? MessageIdUniqueCheck { get; set; }
    }
}