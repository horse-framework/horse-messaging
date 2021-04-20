using System;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Channels
{
    public class ChannelTypeResolver : ITypeDescriptorResolver<ChannelTypeDescriptor>
    {
        public ChannelTypeDescriptor Resolve(Type type, ChannelTypeDescriptor defaultDescriptor)
        {
            throw new NotImplementedException();
        }
    }
}