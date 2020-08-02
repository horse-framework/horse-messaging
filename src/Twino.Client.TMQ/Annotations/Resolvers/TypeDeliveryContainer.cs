using System;
using System.Collections.Generic;

namespace Twino.Client.TMQ.Annotations.Resolvers
{
    /// <summary>
    /// Resolves and keeps delivery descriptors of model types
    /// </summary>
    public class TypeDeliveryContainer : ITypeDeliveryContainer
    {
        private readonly ITypeDeliveryResolver _resolver;

        private readonly Dictionary<Type, TypeDeliveryDescriptor> _deliveryDescriptors;

        /// <summary>
        /// Creates new delivery container
        /// </summary>
        public TypeDeliveryContainer(ITypeDeliveryResolver resolver)
        {
            _resolver = resolver;
            _deliveryDescriptors = new Dictionary<Type, TypeDeliveryDescriptor>();
        }

        /// <summary>
        /// Gets type delivery descriptor for type
        /// </summary>
        public TypeDeliveryDescriptor GetDescriptor<TModel>()
        {
            return GetDescriptor(typeof(TModel));
        }

        /// <summary>
        /// Gets type delivery descriptor for type
        /// </summary>
        public TypeDeliveryDescriptor GetDescriptor(Type type)
        {
            TypeDeliveryDescriptor descriptor;
            _deliveryDescriptors.TryGetValue(type, out descriptor);
            if (descriptor != null)
                return descriptor;

            descriptor = _resolver.Resolve(type);
            _deliveryDescriptors.Add(type, descriptor);

            return descriptor;
        }
    }
}