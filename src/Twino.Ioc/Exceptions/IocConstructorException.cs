using System;

namespace Twino.Ioc.Exceptions
{
    /// <summary>
    /// Thrown when an error occured with constructors of implementation or service types
    /// </summary>
    public class IocConstructorException : Exception
    {
        /// <summary>
        /// Thrown when an error occured with constructors of implementation or service types
        /// </summary>
        public IocConstructorException(string message) : base(message)
        {
        }
    }
}