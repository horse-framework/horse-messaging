using System;
using System.Text.Json.Serialization;
using Twino.MQ.Options;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Build options object with data over network
    /// </summary>
    public class NetworkOptionsBuilder
    {
        #region Properties

        /// <summary>
        /// Acknowledge decisions : "none", "request", "wait" 
        /// </summary>
        [JsonPropertyName("Acknowledge")]
        public string Acknowledge { get; set; }

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        [JsonPropertyName("AcknowledgeTimeout")]
        public int? AcknowledgeTimeout { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
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
        public QueueStatus? Status { get; set; }

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("MessageLimit")]
        public int? MessageLimit { get; set; }

        /// <summary>
        /// Maximum client limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("ClientLimit")]
        public int? ClientLimit { get; set; }

        /// <summary>
        /// Maximum queue limit of the server
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("QueueLimit")]
        public int? QueueLimit { get; set; }

        /// <summary>
        /// Queue auto destroy options
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

        #endregion

        #region Apply

        /// <summary>
        /// Applies non-null values to queue options
        /// </summary>
        public void ApplyToQueue(QueueOptions target)
        {
            if (!string.IsNullOrEmpty(Acknowledge))
                switch (Acknowledge.Trim().ToLower())
                {
                    case "none":
                        target.Acknowledge = QueueAckDecision.None;
                        break;

                    case "request":
                        target.Acknowledge = QueueAckDecision.JustRequest;
                        break;

                    case "wait":
                        target.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                        break;
                }

            if (!string.IsNullOrEmpty(AutoDestroy))
                switch (AutoDestroy.Trim().ToLower())
                {
                    case "disabled":
                        target.AutoDestroy = QueueDestroy.Disabled;
                        break;

                    case "no-message":
                        target.AutoDestroy = QueueDestroy.NoMessages;
                        break;

                    case "no-consumer":
                        target.AutoDestroy = QueueDestroy.NoConsumers;
                        break;

                    case "empty":
                        target.AutoDestroy = QueueDestroy.Empty;
                        break;
                }

            if (AcknowledgeTimeout.HasValue)
                target.AcknowledgeTimeout = TimeSpan.FromMilliseconds(AcknowledgeTimeout.Value);

            if (MessageTimeout.HasValue)
                target.MessageTimeout = TimeSpan.FromMilliseconds(MessageTimeout.Value);

            if (UseMessageId.HasValue)
                target.UseMessageId = UseMessageId.Value;

            if (HideClientNames.HasValue)
                target.HideClientNames = HideClientNames.Value;

            if (Status.HasValue)
                target.Status = Status.Value;

            if (MessageLimit.HasValue)
                target.MessageLimit = MessageLimit.Value;

            if (ClientLimit.HasValue)
                target.ClientLimit = ClientLimit.Value;

            if (DelayBetweenMessages.HasValue)
                target.DelayBetweenMessages = DelayBetweenMessages.Value;

            if (PutBackDelay.HasValue)
                target.PutBackDelay = PutBackDelay.Value;
        }

        #endregion
    }
}