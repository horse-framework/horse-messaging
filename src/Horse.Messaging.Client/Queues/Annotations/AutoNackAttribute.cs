using System;
using Horse.Messaging.Client.Annotations;

namespace Horse.Messaging.Client.Queues.Annotations
{
    /// <summary>
    /// Used on Consumer Interfaces.
    /// Sends negative acknowledge for the message if it's required after consume operation throws an exception
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoNackAttribute : Attribute
    {
        /// <summary>
        /// Reason
        /// </summary>
        public NegativeReason Reason { get; }

        /// <summary>
        /// Creates new negative acknowledge attribute
        /// </summary>
        /// <param name="reason"></param>
        public AutoNackAttribute(NegativeReason reason = NegativeReason.None)
        {
            Reason = reason;
        }
    }
}