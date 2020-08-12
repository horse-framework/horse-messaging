using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Used when queue is created with first push
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class WaitForAcknowledgeAttribute : Attribute
    {
        /// <summary>
        /// If true, queue waits for acknowledge before sending next message
        /// </summary>
        public bool Value { get; }

        /// <summary>
        /// Creates new wait for acknowledge attribute
        /// </summary>
        public WaitForAcknowledgeAttribute(bool value = true)
        {
            Value = value;
        }
    }
}