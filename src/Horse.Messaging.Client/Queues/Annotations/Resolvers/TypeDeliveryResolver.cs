using System;
using System.Collections.Generic;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Annotations.Resolvers;
using Horse.Messaging.Client.Direct.Annotations;

namespace Horse.Messaging.Client.Queues.Annotations.Resolvers
{
    /// <summary>
    /// Resolves model types, checks attributes
    /// and creates delivery descriptor objects.
    /// </summary>
    public class TypeDeliveryResolver : ITypeDeliveryResolver
    {
        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        public TypeDeliveryDescriptor Resolve<TModel>(ModelTypeConfigurator defaultConfigurator)
        {
            return Resolve(typeof(TModel), defaultConfigurator);
        }

        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        public TypeDeliveryDescriptor Resolve(Type type, ModelTypeConfigurator defaultConfigurator)
        {
            TypeDeliveryDescriptor descriptor = new TypeDeliveryDescriptor();

            if (defaultConfigurator != null)
                ResolveDefaults(type, descriptor, defaultConfigurator);

            ResolveBase(type, descriptor);
            ResolveDirect(type, descriptor);
            ResolveQueue(type, descriptor);
            ResolveRouter(type, descriptor);

            return descriptor;
        }

        /// <summary>
        /// Resolves default values from model type configurator
        /// </summary>
        private void ResolveDefaults(Type type, TypeDeliveryDescriptor descriptor, ModelTypeConfigurator defaultConfigurator)
        {
            if (defaultConfigurator.QueueNameFactory != null)
            {
                descriptor.QueueName = defaultConfigurator.QueueNameFactory(type);
                descriptor.HasQueueName = true;
            }

            if (defaultConfigurator.QueueStatus.HasValue)
                descriptor.QueueStatus = defaultConfigurator.QueueStatus.Value;

            if (!string.IsNullOrEmpty(defaultConfigurator.Topic))
                descriptor.Topic = defaultConfigurator.Topic;

            if (defaultConfigurator.PutBackDelay.HasValue)
                descriptor.PutBackDelay = defaultConfigurator.PutBackDelay.Value;

            if (defaultConfigurator.DelayBetweenMessages.HasValue)
                descriptor.DelayBetweenMessages = defaultConfigurator.DelayBetweenMessages.Value;

            if (defaultConfigurator.AckDecision.HasValue)
                descriptor.Acknowledge = defaultConfigurator.AckDecision.Value;

            foreach (Func<KeyValuePair<string, string>> func in defaultConfigurator.HeaderFactories)
                descriptor.Headers.Add(func());
        }

        /// <summary>
        /// Resolves model type for direct messages
        /// </summary>
        private void ResolveDirect(Type type, TypeDeliveryDescriptor descriptor)
        {
            DirectTargetAttribute targetAttribute = type.GetCustomAttribute<DirectTargetAttribute>(true);
            if (targetAttribute != null)
            {
                descriptor.HasDirectReceiver = true;
                descriptor.DirectValue = targetAttribute.Value;
                descriptor.DirectFindBy = targetAttribute.FindBy;
                switch (targetAttribute.FindBy)
                {
                    case FindTargetBy.Id:
                        descriptor.DirectTarget = targetAttribute.Value;
                        break;

                    case FindTargetBy.Name:
                        descriptor.DirectTarget = "@name:" + targetAttribute.Value;
                        break;

                    case FindTargetBy.Type:
                        descriptor.DirectTarget = "@type:" + targetAttribute.Value;
                        break;
                }
            }

            DirectContentTypeAttribute directContentTypeAttribute = type.GetCustomAttribute<DirectContentTypeAttribute>(false);
            if (directContentTypeAttribute != null)
            {
                descriptor.HasContentType = true;
                descriptor.ContentType = directContentTypeAttribute.ContentType;
            }
        }

        /// <summary>
        /// Resolves model type for queue messages
        /// </summary>
        private void ResolveQueue(Type type, TypeDeliveryDescriptor descriptor)
        {
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
        }

        /// <summary>
        /// Resolves model type for router messages
        /// </summary>
        private void ResolveRouter(Type type, TypeDeliveryDescriptor descriptor)
        {
            RouterNameAttribute routerNameAttribute = type.GetCustomAttribute<RouterNameAttribute>(true);
            if (routerNameAttribute != null)
            {
                descriptor.HasRouterName = true;
                descriptor.RouterName = routerNameAttribute.Name;
            }
            else
                descriptor.RouterName = type.FullName;
        }

        /// <summary>
        /// Resolves base and common descriptor attributes of the type and fillds the descriptor object values
        /// </summary>
        private void ResolveBase(Type type, TypeDeliveryDescriptor descriptor)
        {
            descriptor.Type = type;

            HighPriorityMessageAttribute prioAttr = type.GetCustomAttribute<HighPriorityMessageAttribute>(true);
            if (prioAttr != null)
                descriptor.HighPriority = true;

            AcknowledgeAttribute ackAttr = type.GetCustomAttribute<AcknowledgeAttribute>(true);
            if (ackAttr != null)
                descriptor.Acknowledge = ackAttr.Value;

            QueueStatusAttribute statusAttr = type.GetCustomAttribute<QueueStatusAttribute>(true);
            if (statusAttr != null)
                descriptor.QueueStatus = statusAttr.Status;

            QueueTopicAttribute topicAttr = type.GetCustomAttribute<QueueTopicAttribute>(true);
            if (topicAttr != null)
                descriptor.Topic = topicAttr.Topic;

            MessageTimeoutAttribute msgTimeoutAttr = type.GetCustomAttribute<MessageTimeoutAttribute>(true);
            if (msgTimeoutAttr != null)
                descriptor.MessageTimeout = msgTimeoutAttr.Value;

            AcknowledgeTimeoutAttribute ackTimeoutAttr = type.GetCustomAttribute<AcknowledgeTimeoutAttribute>(true);
            if (ackTimeoutAttr != null)
                descriptor.AcknowledgeTimeout = ackTimeoutAttr.Value;

            IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
            foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
                descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));
        }
    }
}