using System;

namespace Twino.Ioc.Exceptions
{
    /// <summary>
    /// Thrown when one of registered services has a constructor unregistered constructor parameter
    /// </summary>
    public class MissingReferenceException : Exception
    {
        /// <summary>
        /// Creates new missing reference exception
        /// </summary>
        public MissingReferenceException(string message) : base(message)
        {
        }
    }
}