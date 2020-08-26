using System;
using System.Collections.Generic;
using System.Reflection;

namespace Twino.Client.TMQ.Annotations.Resolvers
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
        public TypeDeliveryDescriptor Resolve<TModel>()
        {
            return Resolve(typeof(TModel));
        }

        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        public TypeDeliveryDescriptor Resolve(Type type)
        {
            TypeDeliveryDescriptor descriptor = new TypeDeliveryDescriptor();
            ResolveBase(type, descriptor);
            ResolveDirect(type, descriptor);
            ResolveQueue(type, descriptor);
            ResolveRouter(type, descriptor);

            return descriptor;
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

            ContentTypeAttribute contentTypeAttribute = type.GetCustomAttribute<ContentTypeAttribute>(false);
            if (contentTypeAttribute != null)
            {
                descriptor.HasContentType = true;
                descriptor.ContentType = contentTypeAttribute.ContentType;
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
                descriptor.QueueName = queueNameAttribute.Name;
            }
            else
                descriptor.QueueName = type.FullName;
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

            IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
            foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
                descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));
        }
    }
}