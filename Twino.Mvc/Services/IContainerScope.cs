using System;

namespace Twino.Mvc.Services
{
    /// <summary>
    /// Scope implementation for service containers
    /// </summary>
    public interface IContainerScope
    {
        
        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        TService Get<TService>(IServiceContainer services)
            where TService : class;

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        object Get(Type serviceType, IServiceContainer services);
    }
}