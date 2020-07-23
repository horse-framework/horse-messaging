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
            DirectReceiverAttribute receiverAttribute = type.GetCustomAttribute<DirectReceiverAttribute>(false);
            if (receiverAttribute != null)
            {
                descriptor.DirectValue = receiverAttribute.Value;
                descriptor.DirectFindBy = receiverAttribute.FindBy;
                switch (receiverAttribute.FindBy)
                {
                    case FindReceiverBy.Id:
                        descriptor.DirectTarget = receiverAttribute.Value;
                        break;

                    case FindReceiverBy.Name:
                        descriptor.DirectTarget = "@name:" + receiverAttribute.Value;
                        break;

                    case FindReceiverBy.Type:
                        descriptor.DirectTarget = "@type:" + receiverAttribute.Value;
                        break;
                }
            }

            ContentTypeAttribute contentTypeAttribute = type.GetCustomAttribute<ContentTypeAttribute>(false);
            if (contentTypeAttribute != null)
                descriptor.ContentType = contentTypeAttribute.ContentType;
        }

        /// <summary>
        /// Resolves model type for queue messages
        /// </summary>
        private void ResolveQueue(Type type, TypeDeliveryDescriptor descriptor)
        {
            ChannelNameAttribute channelNameAttribute = type.GetCustomAttribute<ChannelNameAttribute>(false);
            if (channelNameAttribute != null)
                descriptor.ChannelName = channelNameAttribute.Channel;
            else
                descriptor.ChannelName = type.FullName;

            QueueIdAttribute queueIdAttribute = type.GetCustomAttribute<QueueIdAttribute>(false);
            if (queueIdAttribute != null)
                descriptor.QueueId = queueIdAttribute.QueueId;
        }

        /// <summary>
        /// Resolves model type for router messages
        /// </summary>
        private void ResolveRouter(Type type, TypeDeliveryDescriptor descriptor)
        {
            RouterNameAttribute routerNameAttribute = type.GetCustomAttribute<RouterNameAttribute>(false);
            if (routerNameAttribute != null)
                descriptor.RouterName = routerNameAttribute.Name;
        }

        /// <summary>
        /// Resolves base and common descriptor attributes of the type and fillds the descriptor object values
        /// </summary>
        private void ResolveBase(Type type, TypeDeliveryDescriptor descriptor)
        {
            descriptor.Type = type;

            HighPriorityMessageAttribute hp = type.GetCustomAttribute<HighPriorityMessageAttribute>(false);
            if (hp != null)
                descriptor.HighPriority = true;

            OnlyFirstAcquirerAttribute of = type.GetCustomAttribute<OnlyFirstAcquirerAttribute>(false);
            if (of != null)
                descriptor.OnlyFirstAcquirer = true;

            IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(false);
            foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
                descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));
        }
    }
}