using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.Ioc.Pool;

namespace Twino.Ioc
{
    /// <summary>
    /// MVC Scope implementation for service container
    /// </summary>
    internal class DefaultContainerScope : IContainerScope
    {
        /// <summary>
        /// All created scoped services in this scope
        /// </summary>
        private Dictionary<Type, object> _scopedServices;

        /// <summary>
        /// All locked pool instances in this scope
        /// </summary>
        private readonly Dictionary<IServicePool, List<PoolServiceDescriptor>> _poolInstances = new Dictionary<IServicePool, List<PoolServiceDescriptor>>();

        /// <summary>
        /// Puts and instance into the scope
        /// </summary>
        /// <param name="serviceType">Item service type</param>
        /// <param name="instance">Item instance</param>
        public void PutItem(Type serviceType, object instance)
        {
            if (_scopedServices == null)
                _scopedServices = new Dictionary<Type, object>();

            lock (_scopedServices)
                _scopedServices.Add(serviceType, instance);
        }

        /// <summary>
        /// Gets the service from the container
        /// </summary>
        public async Task<TService> Get<TService>(IServiceContainer services) where TService : class
        {
            object o = await Get(typeof(TService), services);
            return (TService) o;
        }

        /// <summary>
        /// Gets the service from the container
        /// </summary>
        public async Task<object> Get(Type serviceType, IServiceContainer services)
        {
            ServiceDescriptor descriptor = services.GetDescriptor(serviceType);
            if (descriptor == null)
                throw new KeyNotFoundException($"Service type is not found {serviceType.Name}");

            return await Get(descriptor, services);
        }

        /// <summary>
        /// Gets the service from the container
        /// </summary>
        public async Task<object> Get(ServiceDescriptor descriptor, IServiceContainer services)
        {
            if (_scopedServices == null)
                _scopedServices = new Dictionary<Type, object>();

            //try to get from created instances
            bool found = _scopedServices.TryGetValue(descriptor.ServiceType, out object instance);

            if (found)
                return instance;

            //we couldn't find any created instance. create new.
            if (descriptor.ImplementationFactory != null)
                instance = descriptor.ImplementationFactory(services);
            else
                instance = await services.CreateInstance(descriptor.ImplementationType, descriptor.Constructors, this);

            if (instance is null) return null;

            if (descriptor.AfterCreatedMethod != null)
                descriptor.AfterCreatedMethod.DynamicInvoke(instance);

            if (descriptor.ProxyType != null)
            {
                IServiceProxy p = (IServiceProxy) await services.CreateInstance(descriptor.ProxyType, null, this);
                instance = p.Proxy(instance);
            }

            if (instance != null)
                _scopedServices.Add(descriptor.ServiceType, instance);

            return instance;
        }

        /// <summary>
        /// Adds a pool instance to using list
        /// This instance will be released while disposing
        /// </summary>
        public void UsePoolItem(IServicePool pool, PoolServiceDescriptor descriptor)
        {
            lock (_poolInstances)
            {
                if (_poolInstances.ContainsKey(pool))
                {
                    if (pool.Type == ImplementationType.Transient)
                        _poolInstances[pool].Add(descriptor);
                }
                else
                    _poolInstances.Add(pool, new List<PoolServiceDescriptor> {descriptor});
            }
        }

        /// <summary>
        /// Releases all source and using pool instances
        /// </summary>
        public void Dispose()
        {
            lock (_poolInstances)
            {
                foreach (var kv in _poolInstances)
                {
                    foreach (PoolServiceDescriptor descriptor in kv.Value)
                        kv.Key.Release(descriptor);
                }

                _poolInstances.Clear();
            }
        }
    }
}