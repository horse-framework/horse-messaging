using System;
using System.Collections.Generic;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;

namespace Horse.Messaging.Client.Queues
{
    internal class QueueTypeResolver : ITypeDescriptorResolver<QueueTypeDescriptor>
    {
        private readonly HorseClient _client;

        public QueueTypeResolver(HorseClient client)
        {
            _client = client;
        }

        public QueueTypeDescriptor Resolve(Type type, QueueTypeDescriptor defaultDescriptor)
        {
            QueueTypeDescriptor descriptor = new QueueTypeDescriptor();

            if (defaultDescriptor != null)
                ResolveDefaults(type, descriptor, defaultDescriptor);

            if (_client.Queue.NameHandler != null && !IsConsumerType(type))
            {
                string queueName = _client.Queue.NameHandler.Invoke(new QueueNameHandlerContext
                {
                    Type = type,
                    Client = _client
                });

                if (!string.IsNullOrEmpty(queueName))
                {
                    descriptor.QueueName = queueName;
                    descriptor.HasQueueName = true;
                }
            }

            ResolveDescriptor(type, descriptor);

            return descriptor;
        }

        private bool IsConsumerType(Type type)
        {
            Type[] interfaceTypes = type.GetInterfaces();
            if (interfaceTypes.Length == 0)
                return false;

            Type openGenericType = typeof(IQueueConsumer<>);

            foreach (Type interfaceType in interfaceTypes)
            {
                if (!interfaceType.IsGenericType)
                    continue;

                Type[] genericArgs = interfaceType.GetGenericArguments();
                if (genericArgs.Length != 1)
                    continue;

                Type genericType = openGenericType.MakeGenericType(genericArgs[0]);
                if (type.IsAssignableTo(genericType))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves default values from model type configurator
        /// </summary>
        private void ResolveDefaults(Type type, QueueTypeDescriptor descriptor, QueueTypeDescriptor defaultConfigurator)
        {
            if (defaultConfigurator.QueueType.HasValue)
                descriptor.QueueType = defaultConfigurator.QueueType.Value;

            if (!string.IsNullOrEmpty(defaultConfigurator.Topic))
                descriptor.Topic = defaultConfigurator.Topic;

            if (defaultConfigurator.PutBackDecision.HasValue)
                descriptor.PutBackDecision = defaultConfigurator.PutBackDecision.Value;

            if (defaultConfigurator.PutBackDelay.HasValue)
                descriptor.PutBackDelay = defaultConfigurator.PutBackDelay.Value;

            if (defaultConfigurator.DelayBetweenMessages.HasValue)
                descriptor.DelayBetweenMessages = defaultConfigurator.DelayBetweenMessages.Value;

            if (defaultConfigurator.Acknowledge.HasValue)
                descriptor.Acknowledge = defaultConfigurator.Acknowledge.Value;
        }

        /// <summary>
        /// Resolves base and common descriptor attributes of the type and fillds the descriptor object values
        /// </summary>
        private void ResolveDescriptor(Type type, QueueTypeDescriptor descriptor)
        {
            descriptor.Type = type;

            HighPriorityMessageAttribute prioAttr = type.GetCustomAttribute<HighPriorityMessageAttribute>(true);
            if (prioAttr != null)
                descriptor.HighPriority = true;

            AcknowledgeAttribute ackAttr = type.GetCustomAttribute<AcknowledgeAttribute>(true);
            if (ackAttr != null)
                descriptor.Acknowledge = ackAttr.Value;

            QueueTypeAttribute typeAttr = type.GetCustomAttribute<QueueTypeAttribute>(true);
            if (typeAttr != null)
                descriptor.QueueType = typeAttr.Type;

            QueueTopicAttribute topicAttr = type.GetCustomAttribute<QueueTopicAttribute>(true);
            if (topicAttr != null)
                descriptor.Topic = topicAttr.Topic;

            MessageTimeoutAttribute msgTimeoutAttr = type.GetCustomAttribute<MessageTimeoutAttribute>(true);
            if (msgTimeoutAttr != null)
                descriptor.MessageTimeout = msgTimeoutAttr.Value;

            AcknowledgeTimeoutAttribute ackTimeoutAttr = type.GetCustomAttribute<AcknowledgeTimeoutAttribute>(true);
            if (ackTimeoutAttr != null)
                descriptor.AcknowledgeTimeout = ackTimeoutAttr.Value;

            QueueNameAttribute queueNameAttribute = type.GetCustomAttribute<QueueNameAttribute>(true);
            if (queueNameAttribute != null)
            {
                descriptor.HasQueueName = true;
                descriptor.QueueName = string.IsNullOrEmpty(queueNameAttribute.Name) ? type.Name : queueNameAttribute.Name;
            }
            else if (!descriptor.HasQueueName)
                descriptor.QueueName = type.Name;

            DelayBetweenMessagesAttribute delayAttr = type.GetCustomAttribute<DelayBetweenMessagesAttribute>(true);
            if (delayAttr != null)
                descriptor.DelayBetweenMessages = delayAttr.Value;

            PutBackAttribute putbackAttr = type.GetCustomAttribute<PutBackAttribute>(true);
            if (putbackAttr != null)
            {
                descriptor.PutBackDecision = putbackAttr.Value;
                descriptor.PutBackDelay = putbackAttr.Delay;
            }

            IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
            foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
                descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));
        }
    }
}