using System;

namespace Horse.Mq.Client.Models
{
    internal class TransportExceptionDescriptor
    {
        public Type ModelType { get; set; }
        public Type ExceptionType { get; set; }

        public TransportExceptionDescriptor(Type modelType)
        {
            ModelType = modelType;
        }

        public TransportExceptionDescriptor(Type modelType, Type exceptionType)
        {
            ModelType = modelType;
            ExceptionType = exceptionType;
        }
    }
}