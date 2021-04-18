using System;

namespace Horse.Messaging.Client.Annotations.Resolvers
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
        TypeDeliveryDescriptor Resolve<TModel>(ModelTypeConfigurator defaultConfigurator);

        /// <summary>
        /// Resolves model type descriptor
        /// </summary>
        TypeDeliveryDescriptor Resolve(Type type, ModelTypeConfigurator defaultConfigurator);

    }
}