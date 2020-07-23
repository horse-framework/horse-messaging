using System;

namespace Twino.Client.TMQ.Annotations.Resolvers
{
    /// <summary>
    /// Resolves and keeps delivery descriptors of model types
    /// </summary>
    public interface ITypeDeliveryContainer
    {
        /// <summary>
        /// Gets delivery descriptor for type
        /// </summary>
        TypeDeliveryDescriptor GetDescriptor<TModel>();

        /// <summary>
        /// Gets delivery descriptor for type
        /// </summary>
        TypeDeliveryDescriptor GetDescriptor(Type type);

    }
}