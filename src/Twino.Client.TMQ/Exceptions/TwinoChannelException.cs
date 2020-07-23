using System;

namespace Twino.Client.TMQ.Exceptions
{
    /// <summary>
    /// Thrown when an error occured on a channel operation
    /// </summary>
    public class TwinoChannelException : Exception
    {
        /// <summary>
        /// Creates new TwinoChannelException
        /// </summary>
        public TwinoChannelException(string message) : base(message)
        {
        }
    }
}