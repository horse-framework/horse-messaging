using System;

namespace Twino.MQ.Client.Exceptions
{
    /// <summary>
    /// Thrown when an error occured on queue operations
    /// </summary>
    public class TwinoQueueException : Exception
    {
        /// <summary>
        /// Created new TwinoQueueException
        /// </summary>
        public TwinoQueueException(string message) : base(message)
        {
        }
    }
}