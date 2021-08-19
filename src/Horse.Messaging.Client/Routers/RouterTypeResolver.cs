using System;
using System.Collections.Generic;
using System.Reflection;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Client.Routers.Annotations;

namespace Horse.Messaging.Client.Routers
{
    internal class RouterTypeResolver : ITypeDescriptorResolver<RouterTypeDescriptor>
    {
        public RouterTypeDescriptor Resolve(Type type, RouterTypeDescriptor defaultDescriptor)
        {
            RouterTypeDescriptor descriptor = new RouterTypeDescriptor();

            if (defaultDescriptor != null)
                ResolveDefaults(type, descriptor, defaultDescriptor);

            ResolveDescriptor(type, descriptor);

            return descriptor;
        }

        /// <summary>
        /// Resolves default values from model type configurator
        /// </summary>
        private void ResolveDefaults(Type type, RouterTypeDescriptor descriptor, RouterTypeDescriptor defaultConfigurator)
        {
            descriptor.HighPriority = defaultConfigurator.HighPriority;
            descriptor.ContentType = defaultConfigurator.ContentType;
            descriptor.RouterName = defaultConfigurator.RouterName;

            if (!string.IsNullOrEmpty(defaultConfigurator.Topic))
                descriptor.Topic = defaultConfigurator.Topic;
        }

        /// <summary>
        /// Resolves base and common descriptor attributes of the type and fillds the descriptor object values
        /// </summary>
        private void ResolveDescriptor(Type type, RouterTypeDescriptor descriptor)
        {
            descriptor.Type = type;

            HighPriorityMessageAttribute prioAttr = type.GetCustomAttribute<HighPriorityMessageAttribute>(true);
            if (prioAttr != null)
                descriptor.HighPriority = true;

            DirectContentTypeAttribute contentTypeAttr = type.GetCustomAttribute<DirectContentTypeAttribute>(true);
            if (contentTypeAttr != null)
                descriptor.ContentType = contentTypeAttr.ContentType;

            RouterTopicAttribute topicAttr = type.GetCustomAttribute<RouterTopicAttribute>(true);
            if (topicAttr != null)
                descriptor.Topic = topicAttr.Topic;

            RouterNameAttribute routerNameAttribute = type.GetCustomAttribute<RouterNameAttribute>(true);
            if (routerNameAttribute != null)
            {
                descriptor.HasRouterName = true;
                descriptor.RouterName = string.IsNullOrEmpty(routerNameAttribute.Name) ? type.Name : routerNameAttribute.Name;
            }
            else if (!descriptor.HasRouterName)
                descriptor.RouterName = type.Name;

            IEnumerable<MessageHeaderAttribute> headerAttributes = type.GetCustomAttributes<MessageHeaderAttribute>(true);
            foreach (MessageHeaderAttribute headerAttribute in headerAttributes)
                descriptor.Headers.Add(new KeyValuePair<string, string>(headerAttribute.Key, headerAttribute.Value));
        }
    }
}