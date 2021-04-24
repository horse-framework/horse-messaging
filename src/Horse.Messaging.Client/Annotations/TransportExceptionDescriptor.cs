using System;

namespace Horse.Messaging.Client.Annotations
{
    /// <summary>
    /// Transport exception descriptor is a definition to send exceptions to server
    /// </summary>
    public class TransportExceptionDescriptor
    {
        /// <summary>
        /// Consumed model type
        /// </summary>
        public Type ModelType { get; set; }

        /// <summary>
        /// Type of the thrown exception
        /// </summary>
        public Type ExceptionType { get; set; }

        /// <summary>
        /// Creates new transport exception descriptor
        /// </summary>
        public TransportExceptionDescriptor(Type modelType)
        {
            ModelType = modelType;
        }

        /// <summary>
        /// Creates new transport exception descriptor
        /// </summary>
        public TransportExceptionDescriptor(Type modelType, Type exceptionType)
        {
            ModelType = modelType;
            ExceptionType = exceptionType;
        }
    }
}