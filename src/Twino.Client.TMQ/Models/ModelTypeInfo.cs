using System;
using Twino.Client.TMQ.Internal;

namespace Twino.Client.TMQ.Models
{
    internal class ModelTypeInfo
    {
        public ConsumerMethod Method { get; }
        public Type ModelType { get; }
        public Type ConsumerType { get; }

        public ModelTypeInfo(Type consumerType ,Type modelType, ConsumerMethod method)
        {
            ConsumerType = consumerType;
            ModelType = modelType;
            Method = method;
        }
    }
}