using System;
using Twino.Client.TMQ.Models;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used when queue is created with first push
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueStatusAttribute : Attribute
    {
        /// <summary>
        /// Queue status value
        /// </summary>
        public MessagingQueueStatus Status { get; }

        /// <summary>
        /// Creates new queue status attribute
        /// </summary>
        public QueueStatusAttribute(MessagingQueueStatus status)
        {
            Status = status;
        }
    }
}