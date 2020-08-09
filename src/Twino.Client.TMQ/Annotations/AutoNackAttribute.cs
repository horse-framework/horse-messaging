using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Negative Acknowledge reasons
    /// </summary>
    public enum NackReason
    {
        /// <summary>
        /// Reason none
        /// </summary>
        None,

        /// <summary>
        /// Reason error
        /// </summary>
        Error,

        /// <summary>
        /// Reason is class name of the exception
        /// </summary>
        ExceptionType,

        /// <summary>
        /// Reason is messge of the exception
        /// </summary>
        ExceptionMessage
    }

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
        public NackReason Reason { get; }

        /// <summary>
        /// Creates new negative acknowledge attribute
        /// </summary>
        /// <param name="reason"></param>
        public AutoNackAttribute(NackReason reason = NackReason.None)
        {
            Reason = reason;
        }
    }
}