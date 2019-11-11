using System;
using System.Threading.Tasks;
using Twino.Ioc.Pool;

namespace Twino.Ioc
{
    /// <summary>
    /// Scope implementation for service containers
    /// </summary>
    public interface IContainerScope : IDisposable
    {
        /// <summary>
        /// Puts and instance into the scope
        /// </summary>
        /// <param name="serviceType">Item service type</param>
        /// <param name="instance">Item instance</param>
        void PutItem(Type serviceType, object instance);

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        Task<TService> Get<TService>(IServiceContainer services)
            where TService : class;

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        Task<object> Get(Type serviceType, IServiceContainer services);

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        Task<object> Get(ServiceDescriptor descriptor, IServiceContainer services);

        /// <summary>
        /// When scope uses an instance of pool, operation should be informed with this method.
        /// While scope is disposing, it will tell to pool to release the instance
        /// </summary>
        void UsePoolItem(IServicePool pool, PoolServiceDescriptor descriptor);
    }
}