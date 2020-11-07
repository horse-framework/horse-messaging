using System;

namespace Twino.MQ.Client.Exceptions
{
    /// <summary>
    /// Thrown when an error occured on connections
    /// </summary>
    public class TwinoSocketException : Exception
    {
        /// <summary>
        /// Created new TwinoSocketException
        /// </summary>
        public TwinoSocketException(string message) : base(message)
        {
        }
    }
}