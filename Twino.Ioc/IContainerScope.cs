using System;
using System.Threading.Tasks;

namespace Twino.Ioc
{
    /// <summary>
    /// Scope implementation for service containers
    /// </summary>
    public interface IContainerScope
    {
        
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
    }
}