using System;

namespace Twino.Mvc.Services
{
    /// <summary>
    /// Service container implementation for Dependency Inversion
    /// </summary>
    public interface IServiceContainer
    {

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        void AddTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        void AddTransient(Type serviceType, Type implementationType);

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        void AddScoped<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        void AddScoped(Type serviceType, Type implementationType);
        
        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        void AddSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a singleton service with instance to the container.
        /// </summary>
        void AddSingleton<TService, TImplementation>(TImplementation instance)
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        void AddSingleton(Type serviceType, Type implementationType);

        /// <summary>
        /// Adds a singleton service with instance to the container.
        /// </summary>
        void AddSingleton(Type serviceType, object instance);

        /// <summary>
        /// Removes the service from the container
        /// </summary>
        void Remove<TService>()
            where TService : class;

        /// <summary>
        /// Removes the service from the container
        /// </summary>
        void Remove(Type type);

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        TService Get<TService>(IContainerScope scope = null)
            where TService : class;

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        object Get(Type serviceType, IContainerScope scope = null);
        
        /// <summary>
        /// Gets descriptor of type
        /// </summary>
        ServiceDescriptor GetDescriptor<TService>();
        
        /// <summary>
        /// Gets descriptor of type
        /// </summary>
        ServiceDescriptor GetDescriptor(Type serviceType);

        /// <summary>
        /// Creates new instance of type
        /// </summary>
        object CreateInstance(Type type, IContainerScope scope = null);

        /// <summary>
        /// Creates new scope belong this container.
        /// If your service container does not support scoping, you can throw NotSupportedException
        /// </summary>
        IContainerScope CreateScope();
    }
}
