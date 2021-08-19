using System;

namespace Horse.Messaging.Client.Internal
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TDescriptor"></typeparam>
    public interface ITypeDescriptorResolver<TDescriptor>
    {
        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        TDescriptor Resolve(Type type, TDescriptor defaultDescriptor);
    }
}