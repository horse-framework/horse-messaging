using System;

namespace Twino.Ioc.Exceptions
{
    /// <summary>
    /// Thrown when circular references are registered into service container
    /// </summary>
    public class CircularReferenceException : Exception
    {
        /// <summary>
        /// Creates new circular reference exception
        /// </summary>
        public CircularReferenceException(string message) : base(message)
        {
        }
    }
}