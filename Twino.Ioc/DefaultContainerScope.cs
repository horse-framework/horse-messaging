using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Twino.Ioc
{
    /// <summary>
    /// MVC Scope implementation for service container
    /// </summary>
    internal class DefaultContainerScope : IContainerScope
    {
        private Dictionary<Type, object> _scopedServices;

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public async Task<TService> Get<TService>(IServiceContainer services) where TService : class
        {
            object o = await Get(typeof(TService), services);
            return (TService) o;
        }

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public async Task<object> Get(Type serviceType, IServiceContainer services)
        {
            ServiceDescriptor descriptor = services.GetDescriptor(serviceType);
            return await Get(descriptor, services);
        }

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public async Task<object> Get(ServiceDescriptor descriptor, IServiceContainer services)
        {
            if (_scopedServices == null)
                _scopedServices = new Dictionary<Type, object>();

            //try to get from created instances
            object instance;
            bool found = _scopedServices.TryGetValue(descriptor.ServiceType, out instance);
            if (found)
                return instance;

            //we couldn't find any created instance. create new.
            instance = await services.CreateInstance(descriptor.ImplementationType, this);

            if (instance != null)
                _scopedServices.Add(descriptor.ServiceType, instance);

            return instance;
        }
    }
}