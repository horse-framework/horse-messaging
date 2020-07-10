using System;

namespace Twino.Ioc.Exceptions
{
    /// <summary>
    /// Thrown when there is a scope error for Scoped implementations
    /// </summary>
    public class ScopeException : Exception
    {
        /// <summary>
        /// Creates new scope exception
        /// </summary>
        public ScopeException(string message) : base(message)
        {
        }
    }
}