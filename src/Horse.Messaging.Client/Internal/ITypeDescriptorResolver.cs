using System;

namespace Horse.Messaging.Client.Internal
{
    public interface ITypeDescriptorResolver<TDescriptor>
    {
        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        TDescriptor Resolve(Type type, TDescriptor defaultDescriptor);
    }
}