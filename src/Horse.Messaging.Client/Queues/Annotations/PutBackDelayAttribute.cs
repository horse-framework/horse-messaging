using System;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Put Back Delay attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PutBackDelayAttribute : Attribute
    {
        /// <summary>
        /// Delay in milliseconds
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Creates new put back delay attribute.
        /// Value in milliseconds
        /// </summary>
        public PutBackDelayAttribute(int value)
        {
            Value = value;
        }
    }
}