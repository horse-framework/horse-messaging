using System;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Used to specify acknowledge timeout for the queue
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AcknowledgeTimeoutAttribute : Attribute
    {
        /// <summary>
        /// Acknowledge timeout duration in seconds
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Creates new acknowledge timeout attribute
        /// </summary>
        public AcknowledgeTimeoutAttribute(int seconds)
        {
            Value = seconds;
        }
    }
}