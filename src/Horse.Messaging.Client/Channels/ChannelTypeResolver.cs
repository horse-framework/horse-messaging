using System;
using System.Reflection;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Descriptor for channel subscriber and message models
    /// </summary>
    public class ChannelTypeResolver : ITypeDescriptorResolver<ChannelTypeDescriptor>
    {
        public ChannelTypeDescriptor Resolve(Type type, ChannelTypeDescriptor defaultDescriptor)
        {
            ChannelTypeDescriptor descriptor = new ChannelTypeDescriptor();
            descriptor.Name = defaultDescriptor?.Name;

            ChannelNameAttribute nameAttr = type.GetCustomAttribute<ChannelNameAttribute>();
            if (nameAttr != null)
                descriptor.Name = nameAttr.Name;

            return descriptor;
        }
    }
}