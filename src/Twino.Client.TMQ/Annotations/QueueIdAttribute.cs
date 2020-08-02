using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Queue Id attribute for queue messages.
    /// Used for finding the queues for types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueIdAttribute : Attribute
    {
        /// <summary>
        /// The Queue Id for the type
        /// </summary>
        public ushort QueueId { get; }

        /// <summary>
        /// Creates new Queue Id attribute
        /// </summary>
        public QueueIdAttribute(ushort queueId)
        {
            QueueId = queueId;
        }
    }
}