using System;
using Twino.MQ.Queues;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class QueueOptions
    {
        /// <summary>
        /// Acknowledge decision. Default is wait for acknowledge.
        /// </summary>
        public QueueAckDecision Acknowledge { get; set; } = QueueAckDecision.WaitForAcknowledge;

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
        /// Maximum client limit of the channel
        /// Zero is unlimited
        /// </summary>
        public int ClientLimit { get; set; }

        /// <summary>
        /// Channel auto destroy options. Default value is NoMessagesAndConsumers.
        /// </summary>
        public QueueDestroy AutoDestroy { get; set; } = QueueDestroy.Empty;

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
        public static QueueOptions CloneFrom(QueueOptions options)
        {
            return new QueueOptions
                   {
                       Status = options.Status,
                       AcknowledgeTimeout = options.AcknowledgeTimeout,
                       MessageTimeout = options.MessageTimeout,
                       Acknowledge = options.Acknowledge,
                       HideClientNames = options.HideClientNames,
                       UseMessageId = options.UseMessageId,
                       MessageLimit = options.MessageLimit,
                       MessageSizeLimit = options.MessageSizeLimit
                   };
        }
    }
}