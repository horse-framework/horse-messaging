using System;
using System.Collections.Generic;

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
            if (_scopedServices == null)
                _scopedServices = new Dictionary<Type, object>();

            ServiceDescriptor descriptor = services.GetDescriptor(serviceType);

            //try to get from created instances
            object instance;
            bool found = _scopedServices.TryGetValue(serviceType, out instance);
            if (found)
                return instance;

            //we couldn't find any created instance. create new.
            instance = services.CreateInstance(descriptor.ImplementationType, this);

            if (instance != null)
                _scopedServices.Add(serviceType, instance);

            return instance;
        }
    }
}