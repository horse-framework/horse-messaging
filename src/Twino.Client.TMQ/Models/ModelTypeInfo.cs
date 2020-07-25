using System;

namespace Twino.Client.TMQ.Models
{
    internal class ModelTypeInfo
    {
        public bool IsQueueConsumer { get; }
        public Type ModelType { get; }
        public Type ConsumerType { get; }

        public ModelTypeInfo(Type consumerType ,Type modelType, bool isQueueConsumer)
        {
            ConsumerType = consumerType;
            ModelType = modelType;
            IsQueueConsumer = isQueueConsumer;
        }
    }
}