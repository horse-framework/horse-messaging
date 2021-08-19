using System;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Options
{
    /// <summary>
    /// Queue options
    /// </summary>
    public class QueueOptions
    {
        /// <summary>
        /// Acknowledge decision. Default is just request.
        /// </summary>
        public QueueAckDecision Acknowledge { get; set; } = QueueAckDecision.JustRequest;

        /// <summary>
        /// When acknowledge is required, maximum duration for waiting acknowledge message
        /// </summary>
        public TimeSpan AcknowledgeTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Default type for the queue
        /// </summary>
        public QueueType Type { get; set; } = QueueType.Push;

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
        /// Maximum client limit of the queue
        /// Zero is unlimited
        /// </summary>
        public int ClientLimit { get; set; }

        /// <summary>
        /// Waits in milliseconds after sending each message to it's consumers.
        /// Zero is no delay.
        /// </summary>
        public int DelayBetweenMessages { get; set; }

        /// <summary>
        /// Waits in milliseconds before putting message back into the queue.
        /// Zero is no delay.
        /// </summary>
        public int PutBackDelay { get; set; }

        /// <summary>
        /// Queue auto destroy options. Default value is NoMessagesAndConsumers.
        /// </summary>
        public QueueDestroy AutoDestroy { get; set; } = QueueDestroy.Disabled;

        /// <summary>
        /// Creates clone of the object
        /// </summary>
        /// <returns></returns>
        internal object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// Clones queue options from another options
        /// </summary>
        public static QueueOptions CloneFrom(QueueOptions options)
        {
            return new QueueOptions
                   {
                       Type = options.Type,
                       AcknowledgeTimeout = options.AcknowledgeTimeout,
                       MessageTimeout = options.MessageTimeout,
                       Acknowledge = options.Acknowledge,
                       MessageLimit = options.MessageLimit,
                       MessageSizeLimit = options.MessageSizeLimit,
                       DelayBetweenMessages = options.DelayBetweenMessages,
                       PutBackDelay = options.PutBackDelay
                   };
        }
    }
}