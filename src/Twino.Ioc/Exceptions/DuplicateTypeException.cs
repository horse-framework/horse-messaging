using System;

namespace Twino.Ioc.Exceptions
{
    /// <summary>
    /// Thrown when same service type is trying to add to same container multiple times
    /// </summary>
    public class DuplicateTypeException : Exception
    {
        /// <summary>
        /// Creates new duplicate type exception
        /// </summary>
        public DuplicateTypeException(string message) : base(message)
        {
        }
    }
}