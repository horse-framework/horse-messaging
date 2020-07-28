using System;

namespace Twino.Client.TMQ.Models
{
    internal class ModelTypeInfo
    {
        public ReadSource Source { get; }
        public Type ModelType { get; }
        public Type ConsumerType { get; }
        public Type ResponseType { get; }

        public ModelTypeInfo(Type consumerType, ReadSource source, Type modelType, Type responseType = null)
        {
            ConsumerType = consumerType;
            ModelType = modelType;
            Source = source;
            ResponseType = responseType;
        }
    }
}