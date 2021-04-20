using System;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Thrown when an error occured on connections
    /// </summary>
    public class HorseSocketException : Exception
    {
        /// <summary>
        /// Created new HorseSocketException
        /// </summary>
        public HorseSocketException(string message) : base(message)
        {
        }
    }
}