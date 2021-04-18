using System;

namespace Horse.Messaging.Client.Exceptions
{
    /// <summary>
    /// Thrown when an error occured on queue operations
    /// </summary>
    public class HorseQueueException : Exception
    {
        /// <summary>
        /// Created new HorseQueueException
        /// </summary>
        public HorseQueueException(string message) : base(message)
        {
        }
    }
}