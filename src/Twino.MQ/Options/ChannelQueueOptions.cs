using System;
using Twino.MQ.Queues;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class ChannelQueueOptions
    {
        /// <summary>
        /// Queue tag name
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        public bool SendOnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request acknowledge from receivers
        /// </summary>
        public bool RequestAcknowledge { get; set; } = true;

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        public bool UseMessageId { get; set; } = true;

        /// <summary>
        /// If true, queue does not send next message to receivers until acknowledge message received
        /// </summary>
        public bool WaitForAcknowledge { get; set; }

        /// <summary>
        /// If true, server doesn't send client name to receivers in queueus.
        /// </summary>
        public bool HideClientNames { get; set; }

        /// <summary>
        /// Default status for the queue
        /// </summary>
        public QueueStatus Status { get; set; } = QueueStatus.Push;

        /// <summary>
        /// Maximum message limit of the queue
        /// Zero is unlimited
        /// </summary>
        public int MessageLimit { get; set; }

        /// <summary>
        /// Maximum message size limit
        /// Zero is unlimited
        /// </summary>
        public ulong MessageSizeLimit { get; set; }

        /// <summary>
        /// Creates clone of the object
        /// </summary>
        /// <returns></returns>
        internal object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// Clones channel queue options from another options
        /// </summary>
        public static ChannelQueueOptions CloneFrom(ChannelQueueOptions options)
        {
            return new ChannelQueueOptions
                   {
                       Status = options.Status,
                       TagName = options.TagName,
                       AcknowledgeTimeout = options.AcknowledgeTimeout,
                       MessageTimeout = options.MessageTimeout,
                       RequestAcknowledge = options.RequestAcknowledge,
                       HideClientNames = options.HideClientNames,
                       UseMessageId = options.UseMessageId,
                       WaitForAcknowledge = options.WaitForAcknowledge,
                       SendOnlyFirstAcquirer = options.SendOnlyFirstAcquirer,
                       MessageLimit = options.MessageLimit,
                       MessageSizeLimit = options.MessageSizeLimit
                   };
        }
    }
}