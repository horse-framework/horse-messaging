using System;

namespace Twino.Client.TMQ.Annotations
{
    /// <summary>
    /// Publishes exceptions to routers when thrown by consumer objects
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class PublishExceptionsAttribute : Attribute
    {
        /// <summary>
        /// Exception type
        /// </summary>
        public Type ExceptionType { get; }
        
        /// <summary>
        /// Router name
        /// </summary>
        public string RouterName { get; }
        
        /// <summary>
        /// Content Type
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Publishes all exceptions
        /// </summary>
        public PublishExceptionsAttribute(string routerName, ushort contentType = 0)
        {
            RouterName = routerName;
            ContentType = contentType;
        }

        /// <summary>
        /// Publishes specified type of exceptions
        /// </summary>
        public PublishExceptionsAttribute(Type exceptionType, string routerName, ushort contentType = 0)
        {
            ExceptionType = exceptionType;
            RouterName = routerName;
            ContentType = contentType;
        }
    }
}