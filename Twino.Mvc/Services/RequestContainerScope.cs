using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Twino.Mvc.Services
{
    /// <summary>
    /// MVC Scope implementation for service container
    /// </summary>
    internal class RequestContainerScope : IContainerScope
    {
        private Dictionary<Type, object> _scopedServices;
        
        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public TService Get<TService>(IServiceContainer services) where TService : class
        {
            return (TService) Get(typeof(TService), services);
        }

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public object Get(Type serviceType, IServiceContainer services)
        {
            ServiceDescriptor descriptor = services.GetDescriptor(serviceType);
            return Get(descriptor, services);
        }
        
        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public object Get(ServiceDescriptor descriptor, IServiceContainer services)
        {
            if (_scopedServices == null)
                _scopedServices = new Dictionary<Type, object>();

            //try to get from created instances
            object instance;
            bool found = _scopedServices.TryGetValue(descriptor.ServiceType, out instance);
            if (found)
                return instance;

            //we couldn't find any created instance. create new.
            instance = services.CreateInstance(descriptor.ImplementationType, this);

            if (instance != null)
                _scopedServices.Add(descriptor.ServiceType, instance);

            return instance;
        }
    }
}