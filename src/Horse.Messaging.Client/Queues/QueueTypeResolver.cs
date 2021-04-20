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
        public QueueTypeDescriptor Resolve(Type type, QueueTypeDescriptor defaultDescriptor)
        {
            QueueTypeDescriptor descriptor = new QueueTypeDescriptor();

            if (defaultDescriptor != null)
                ResolveDefaults(type, descriptor, defaultDescriptor);

            ResolveDescriptor(type, descriptor);

            return descriptor;
        }


        /// <summary>
        /// Resolves default values from model type configurator
        /// </summary>
        private void ResolveDefaults(Type type, QueueTypeDescriptor descriptor, QueueTypeDescriptor defaultConfigurator)
        {
            //todo: queue name factory
            //if (defaultConfigurator.QueueNameFactory != null)
            //{
            //    descriptor.QueueName = defaultConfigurator.QueueNameFactory(type);
            //    descriptor.HasQueueName = true;
            //}

            if (defaultConfigurator.QueueType.HasValue)
                descriptor.QueueType = defaultConfigurator.QueueType.Value;

            if (!string.IsNullOrEmpty(defaultConfigurator.Topic))
                descriptor.Topic = defaultConfigurator.Topic;

            if (defaultConfigurator.PutBackDelay.HasValue)
                descriptor.PutBackDelay = defaultConfigurator.PutBackDelay.Value;

            if (defaultConfigurator.DelayBetweenMessages.HasValue)
                descriptor.DelayBetweenMessages = defaultConfigurator.DelayBetweenMessages.Value;

            if (defaultConfigurator.Acknowledge.HasValue)
                descriptor.Acknowledge = defaultConfigurator.Acknowledge.Value;

            //todo: header factories
            //foreach (Func<KeyValuePair<string, string>> func in defaultConfigurator.HeaderFactories)
            //    descriptor.Headers.Add(func());
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

            PutBackDelayAttribute putbackAttr = type.GetCustomAttribute<PutBackDelayAttribute>(true);
            if (putbackAttr != null)
                descriptor.PutBackDelay = putbackAttr.Value;

            IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
            foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
                descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));
        }
    }
}