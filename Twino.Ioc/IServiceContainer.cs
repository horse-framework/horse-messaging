using System;
using System.Threading.Tasks;
using Twino.Ioc.Pool;

namespace Twino.Ioc
{
    /// <summary>
    /// Service container implementation for Dependency Inversion
    /// </summary>
    public interface IServiceContainer
    {
        #region Add Transient

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

        #endregion

        #region Add Scoped

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

        #endregion

        #region Add Singleton

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
        void AddSingleton<TService>(TService instance)
            where TService : class;

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        void AddSingleton(Type serviceType, Type implementationType);

        /// <summary>
        /// Adds a singleton service with instance to the container.
        /// </summary>
        void AddSingleton(Type serviceType, object instance);

        #endregion

        #region Add Pool

        /// <summary>
        /// Adds a transient service pool to the container
        /// </summary>
        void AddTransientPool<TService>()
            where TService : class;

        /// <summary>
        /// Adds a scoped service pool to the container
        /// </summary>
        void AddScopedPool<TService>()
            where TService : class;

        /// <summary>
        /// Adds a transient service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        void AddTransientPool<TService>(Action<ServicePoolOptions> options)
            where TService : class;

        /// <summary>
        /// Adds a scoped service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        void AddScopedPool<TService>(Action<ServicePoolOptions> options)
            where TService : class;

        /// <summary>
        /// Adds a transient service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        void AddTransientPool<TService>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class;

        /// <summary>
        /// Adds a scoped service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        void AddScopedPool<TService>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class;

        /// <summary>
        /// Adds a transient service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        void AddTransientPool<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a scoped service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        void AddScopedPool<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a transient service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        void AddTransientPool<TService, TImplementation>(Action<ServicePoolOptions> options)
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a scoped service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        void AddScopedPool<TService, TImplementation>(Action<ServicePoolOptions> options)
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a transient service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        void AddTransientPool<TService, TImplementation>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService;

        /// <summary>
        /// Adds a scoped service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        void AddScopedPool<TService, TImplementation>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService;

        #endregion

        #region Remove - Release

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
        /// Releases item from pool's locked item list
        /// </summary>
        void ReleasePoolItem<TService>(TService service);

        #endregion

        #region Get

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        Task<TService> Get<TService>(IContainerScope scope = null)
            where TService : class;

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        Task<object> Get(Type serviceType, IContainerScope scope = null);

        /// <summary>
        /// Gets descriptor of type
        /// </summary>
        ServiceDescriptor GetDescriptor<TService>();

        /// <summary>
        /// Gets descriptor of type
        /// </summary>
        ServiceDescriptor GetDescriptor(Type serviceType);

        #endregion

        #region Instance - Scope

        /// <summary>
        /// Creates new instance of type
        /// </summary>
        Task<object> CreateInstance(Type type, IContainerScope scope = null);

        /// <summary>
        /// Creates new scope belong this container.
        /// If your service container does not support scoping, you can throw NotSupportedException
        /// </summary>
        IContainerScope CreateScope();

        #endregion
    }
}