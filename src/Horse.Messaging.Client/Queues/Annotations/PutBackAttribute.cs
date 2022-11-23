using System;
using System.ComponentModel;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Message put back decisions for acknowledge failed messages
    /// </summary>
    public enum PutBack
    {
        /// <summary>
        /// Throw message, do not put it back to the queue
        /// </summary>
        [Description("no")]
        No,

        /// <summary>
        /// Put the message back to the end of the queue
        /// </summary>
        [Description("regular")]
        Regular,

        /// <summary>
        /// Put the message back to the queue as priority message
        /// </summary>
        [Description("priority")]
        Priority
    }

    /// <summary>
    /// Put Back Delay attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PutBackAttribute : Attribute
    {
        /// <summary>
        /// Put back decision
        /// </summary>
        public PutBack Value { get; }

        /// <summary>
        /// Delay in milliseconds
        /// </summary>
        public int Delay { get; }

        /// <summary>
        /// Creates new put back delay attribute.
        /// Value in milliseconds
        /// </summary>
        public PutBackAttribute(PutBack value, int delay = 0)
        {
            Value = value;
            Delay = delay;
        }
    }
}