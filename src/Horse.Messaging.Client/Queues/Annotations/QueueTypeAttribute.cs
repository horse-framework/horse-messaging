using System;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Used when queue is created with first push
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueTypeAttribute : Attribute
    {
        /// <summary>
        /// Queue status value
        /// </summary>
        public MessagingQueueType Type { get; }

        /// <summary>
        /// Creates new queue status attribute
        /// </summary>
        public QueueTypeAttribute(MessagingQueueType type)
        {
            Type = type;
        }
    }
}