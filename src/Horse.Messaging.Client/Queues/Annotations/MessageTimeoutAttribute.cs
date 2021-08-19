using System;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Used to specify message timeout for the queue
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MessageTimeoutAttribute : Attribute
    {
        /// <summary>
        /// Message timeout duration in seconds
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Creates new message timeout attribute
        /// </summary>
        public MessageTimeoutAttribute(int seconds)
        {
            Value = seconds;
        }
    }
}