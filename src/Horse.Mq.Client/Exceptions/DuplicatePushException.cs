using System;

namespace Horse.Mq.Client.Exceptions
{
    /// <summary>
    /// Throw when reigstered same type of Exception with PushExceptions attribute
    /// </summary>
    public class DuplicatePushException : Exception
    {
        /// <summary>
        /// Creates new DuplicatePushException
        /// </summary>
        public DuplicatePushException(string message) : base(message)
        {
        }
    }
}