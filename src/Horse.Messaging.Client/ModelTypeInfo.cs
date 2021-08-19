using System;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client
{
    internal class ModelTypeInfo
    {
        public ConsumeSource Source { get; }
        public Type ModelType { get; }
        public Type ConsumerType { get; }
        public Type ResponseType { get; }

        public ModelTypeInfo(Type consumerType, ConsumeSource source, Type modelType, Type responseType = null)
        {
            ConsumerType = consumerType;
            ModelType = modelType;
            Source = source;
            ResponseType = responseType;
        }
    }
}