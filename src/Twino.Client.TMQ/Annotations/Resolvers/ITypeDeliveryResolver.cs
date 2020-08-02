using System;

namespace Twino.Client.TMQ.Annotations.Resolvers
{
    /// <summary>
    /// Resolves model types, checks attributes
    /// and creates delivery descriptor objects.
    /// </summary>
    public interface ITypeDeliveryResolver
    {
        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        TypeDeliveryDescriptor Resolve<TModel>();

        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        TypeDeliveryDescriptor Resolve(Type type);

    }
}