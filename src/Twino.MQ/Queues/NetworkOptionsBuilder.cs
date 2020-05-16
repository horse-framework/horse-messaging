using System;
using System.Text.Json.Serialization;
using Twino.MQ.Options;

namespace Twino.MQ.Queues
{
    /// <summary>
    /// Build options object with data over network
    /// </summary>
    public class NetworkOptionsBuilder
    {
        #region Properties

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
        public QueueStatus? Status { get; set; }

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("MessageLimit")]
        public int? MessageLimit { get; set; }

        /// <summary>
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("ClientLimit")]
        public int? ClientLimit { get; set; }

        /// <summary>
        /// Maximum queue limit of the channel
        /// Zero is unlimited
        /// </summary>
        [JsonPropertyName("QueueLimit")]
        public int? QueueLimit { get; set; }

        /// <summary>
        /// If true, channel will be destroyed when there are no messages in queues and there are no consumers available
        /// </summary>
        [JsonPropertyName("DestroyWhenEmpty")]
        public bool? DestroyWhenEmpty { get; set; }

        /// <summary>
        /// Registry key for message delivery handler
        /// </summary>
        [JsonPropertyName("MessageDeliveryHandler")]
        public string MessageDeliveryHandler { get; set; }

        /// <summary>
        /// Registry key for channel event handler
        /// </summary>
        [JsonPropertyName("ChannelEventHandler")]
        public string ChannelEventHandler { get; set; }

        /// <summary>
        /// Registry key for channel authenticator
        /// </summary>
        [JsonPropertyName("Authenticator")]
        public string ChannelAuthenticator { get; set; }

        /// <summary>
        /// If true, channel supports multiple queues
        /// </summary>
        [JsonPropertyName("AllowMultipleQueues")]
        public bool? AllowMultipleQueues { get; set; }

        /// <summary>
        /// Allowed queues for channel
        /// </summary>
        [JsonPropertyName("AllowedQueues")]
        public ushort[] AllowedQueues { get; set; }

        #endregion

        #region Apply

        /// <summary>
        /// Applies non-null values to channel queue options
        /// </summary>
        public void ApplyToQueue(ChannelQueueOptions target)
        {
            if (SendOnlyFirstAcquirer.HasValue)
                target.SendOnlyFirstAcquirer = SendOnlyFirstAcquirer.Value;

            if (RequestAcknowledge.HasValue)
                target.RequestAcknowledge = RequestAcknowledge.Value;

            if (AcknowledgeTimeout.HasValue)
                target.AcknowledgeTimeout = TimeSpan.FromMilliseconds(AcknowledgeTimeout.Value);

            if (MessageTimeout.HasValue)
                target.MessageTimeout = TimeSpan.FromMilliseconds(MessageTimeout.Value);

            if (UseMessageId.HasValue)
                target.UseMessageId = UseMessageId.Value;

            if (WaitForAcknowledge.HasValue)
                target.WaitForAcknowledge = WaitForAcknowledge.Value;

            if (HideClientNames.HasValue)
                target.HideClientNames = HideClientNames.Value;

            if (Status.HasValue)
                target.Status = Status.Value;
        }

        /// <summary>
        /// Applies non-null values to channel options
        /// </summary>
        public void ApplyToChannel(ChannelOptions target)
        {
            ApplyToQueue(target);

            if (AllowMultipleQueues.HasValue)
                target.AllowMultipleQueues = AllowMultipleQueues.Value;

            if (AllowedQueues != null)
                target.AllowedQueues = AllowedQueues;
        }

        #endregion
    }
}