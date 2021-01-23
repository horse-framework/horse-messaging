using System;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Annotations
{
    /// <summary>
    /// Used when queue is created with first push
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AcknowledgeAttribute : Attribute
    {
        /// <summary>
        /// Queue acknowledge decision
        /// </summary>
        public QueueAckDecision Value { get; }

        /// <summary>
        /// Creates new acknowledge attribute
        /// </summary>
        public AcknowledgeAttribute(QueueAckDecision value)
        {
            Value = value;
        }
    }
}