using System;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Delay between messages in milliseconds.
    /// Useful when wait for acknowledge is disabled but you need to prevent overheat on consumers if producer pushes too many messages in a short duration.
    /// Zero is no delay.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DelayBetweenMessagesAttribute : Attribute
    {
        /// <summary>
        /// Delay value in milliseconds
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Creates new Delay Between Messages attribute
        /// </summary>
        public DelayBetweenMessagesAttribute(int value)
        {
            Value = value;
        }
    }
}