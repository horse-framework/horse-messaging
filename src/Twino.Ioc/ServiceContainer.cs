using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Ioc.Pool;

namespace Twino.Ioc
{
    /// <summary>
    /// Default service container of Twino MVC for dependency inversion
    /// </summary>
    public class ServiceContainer : IServiceContainer
    {
        /// <summary>
        /// Service descriptor items
        /// </summary>
        private Dictionary<Type, ServiceDescriptor> Items { get; set; }

        /// <summary>
        /// Creates new service container
        /// </summary>
        public ServiceContainer()
        {
            Items = new Dictionary<Type, ServiceDescriptor>();
        }

        #region Add Transient

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            AddTransient(typeof(TService), typeof(TImplementation));
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient<TService, TImplementation, TProxy>()
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddTransient(typeof(TService), typeof(TImplementation), typeof(TProxy));
        }


        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient<TService, TImplementation>(Action<TImplementation> afterCreated)
            where TService : class
            where TImplementation : class, TService
        {
            AddTransient(typeof(TService), typeof(TImplementation), null, afterCreated);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient<TService, TImplementation, TProxy>(Action<TImplementation> afterCreated)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddTransient(typeof(TService), typeof(TImplementation), typeof(TProxy), afterCreated);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient(Type serviceType, Type implementationType)
        {
            AddTransient(serviceType, implementationType, null, null);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient(Type serviceType, Type implementationType, Type proxyType)
        {
            AddTransient(serviceType, implementationType, proxyType, null);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddTransient(Type serviceType, Type implementationType, Type proxyType, Delegate afterCreated)
        {
            if (Items.ContainsKey(serviceType))
                throw new InvalidOperationException($"Specified service type is already added into service container: {serviceType.Name}");

            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                ProxyType = proxyType,
                Instance = null,
                ProxyInstance = null,
                Implementation = ImplementationType.Transient,
                AfterCreatedMethod = afterCreated
            };

            Items.Add(serviceType, descriptor);
        }

        #endregion

        #region Add Scoped

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            AddScoped(typeof(TService), typeof(TImplementation));
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped<TService, TImplementation, TProxy>()
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddScoped(typeof(TService), typeof(TImplementation), typeof(TProxy));
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped<TService, TImplementation>(Action<TImplementation> afterCreated)
            where TService : class
            where TImplementation : class, TService
        {
            AddScoped(typeof(TService), typeof(TImplementation), null, afterCreated);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped<TService, TImplementation, TProxy>(Action<TImplementation> afterCreated)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddScoped(typeof(TService), typeof(TImplementation), typeof(TProxy), afterCreated);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped(Type serviceType, Type implementationType)
        {
            AddScoped(serviceType, implementationType, null, null);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped(Type serviceType, Type implementationType, Type proxyType)
        {
            AddScoped(serviceType, implementationType, proxyType, null);
        }

        /// <summary>
        /// Adds a service to the container
        /// </summary>
        public void AddScoped(Type serviceType, Type implementationType, Type proxyType, Delegate afterCreated)
        {
            if (Items.ContainsKey(serviceType))
                throw new InvalidOperationException("Specified service type is already added into service container");

            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                ProxyType = proxyType,
                Instance = null,
                ProxyInstance = null,
                Implementation = ImplementationType.Scoped,
                AfterCreatedMethod = afterCreated
            };

            Items.Add(serviceType, descriptor);
        }

        #endregion

        #region Add Singleton

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            AddSingleton(typeof(TService), typeof(TImplementation));
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton<TService, TImplementation, TProxy>()
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddSingleton(typeof(TService), typeof(TImplementation), typeof(TProxy));
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton<TService, TImplementation>(Action<TImplementation> afterCreated)
            where TService : class
            where TImplementation : class, TService
        {
            AddSingleton(typeof(TService), typeof(TImplementation), afterCreated);
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton<TService, TImplementation, TProxy>(Action<TImplementation> afterCreated)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddSingleton(typeof(TService), typeof(TImplementation), typeof(TProxy), afterCreated);
        }

        /// <summary>
        /// Adds a singleton service with instance to the container.
        /// </summary>
        public void AddSingleton<TService, TImplementation>(TImplementation instance)
            where TService : class
            where TImplementation : class, TService
        {
            AddSingleton(typeof(TService), instance);
        }

        /// <summary>
        /// Adds a singleton service with instance to the container.
        /// </summary>
        public void AddSingleton<TService>(TService instance)
            where TService : class
        {
            AddSingleton<TService, TService>(instance);
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton(Type serviceType, Type implementationType)
        {
            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                Instance = null,
                Implementation = ImplementationType.Singleton
            };

            Items.Add(serviceType, descriptor);
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton(Type serviceType, Type implementationType, Type proxyType)
        {
            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                ProxyType = proxyType,
                ProxyInstance = null,
                Instance = null,
                Implementation = ImplementationType.Singleton
            };

            Items.Add(serviceType, descriptor);
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton(Type serviceType, Type implementationType, Delegate afterCreated)
        {
            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                Instance = null,
                Implementation = ImplementationType.Singleton,
                AfterCreatedMethod = afterCreated
            };

            Items.Add(serviceType, descriptor);
        }

        /// <summary>
        /// Adds a singleton service to the container.
        /// Service will be created with first call.
        /// </summary>
        public void AddSingleton(Type serviceType, Type implementationType, Type proxyType, Delegate afterCreated)
        {
            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                ProxyType = proxyType,
                Instance = null,
                ProxyInstance = null,
                Implementation = ImplementationType.Singleton,
                AfterCreatedMethod = afterCreated
            };

            Items.Add(serviceType, descriptor);
        }

        /// <summary>
        /// Adds a singleton service with instance to the container.
        /// </summary>
        public void AddSingleton(Type serviceType, object instance)
        {
            Type implementationType = instance.GetType();

            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                Instance = instance,
                Implementation = ImplementationType.Singleton
            };

            Items.Add(serviceType, descriptor);
        }

        #endregion

        #region Add Pool

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        public void AddTransientPool<TService>() where TService : class
        {
            AddTransientPool<TService, TService>(null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        public void AddTransientPool<TService>(Action<ServicePoolOptions> options)
            where TService : class
        {
            AddPool<TService, TService>(ImplementationType.Transient, options, null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        public void AddTransientPool<TService>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
        {
            AddPool<TService, TService>(ImplementationType.Transient, options, instance);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        public void AddTransientPool<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            AddTransientPool<TService, TImplementation>(null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        public void AddTransientPool<TService, TImplementation>(Action<ServicePoolOptions> options)
            where TService : class
            where TImplementation : class, TService
        {
            AddPool<TService, TImplementation>(ImplementationType.Transient, options, null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        public void AddTransientPool<TService, TImplementation, TProxy>(Action<ServicePoolOptions> options)
           where TService : class
           where TImplementation : class, TService
           where TProxy : class, IServiceProxy
        {
            AddPool<TService, TImplementation, TProxy>(ImplementationType.Transient, options, null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        public void AddTransientPool<TService, TImplementation>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService
        {
            AddPool<TService, TImplementation>(ImplementationType.Transient, options, instance);
        }


        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        public void AddTransientPool<TService, TImplementation, TProxy>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddPool<TService, TImplementation, TProxy>(ImplementationType.Transient, options, instance);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        public void AddScopedPool<TService>() where TService : class
        {
            AddScopedPool<TService, TService>(null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        public void AddScopedPool<TService>(Action<ServicePoolOptions> options)
            where TService : class
        {
            AddPool<TService, TService>(ImplementationType.Scoped, options, null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        public void AddScopedPool<TService>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
        {
            AddPool<TService, TService>(ImplementationType.Scoped, options, instance);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        public void AddScopedPool<TService, TImplementation>() where TService : class where TImplementation : class, TService
        {
            AddScopedPool<TService, TImplementation>(null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        public void AddTransientPool<TService, TImplementation, TProxy>()
           where TService : class
           where TImplementation : class, TService
           where TProxy : class, IServiceProxy
        {
            AddScopedPool<TService, TImplementation, TProxy>(null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        public void AddScopedPool<TService, TImplementation, TProxy>()
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddScopedPool<TService, TImplementation, TProxy>(null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        public void AddScopedPool<TService, TImplementation>(Action<ServicePoolOptions> options)
            where TService : class
            where TImplementation : class, TService
        {
            AddPool<TService, TImplementation>(ImplementationType.Scoped, options, null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        public void AddScopedPool<TService, TImplementation, TProxy>(Action<ServicePoolOptions> options)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddPool<TService, TImplementation, TProxy>(ImplementationType.Scoped, options, null);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        public void AddScopedPool<TService, TImplementation>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService
        {
            AddPool<TService, TImplementation>(ImplementationType.Scoped, options, instance);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        public void AddScopedPool<TService, TImplementation, TProxy>(Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            AddPool<TService, TImplementation, TProxy>(ImplementationType.Scoped, options, instance);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="type">Implementation type</param>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        private void AddPool<TService, TImplementation>(ImplementationType type, Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService
        {
            ServicePool<TService, TImplementation> pool = new ServicePool<TService, TImplementation>(type, this, options, instance);
            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                IsPool = true,
                ServiceType = typeof(TService),
                ImplementationType = typeof(ServicePool<TService, TImplementation>),
                Instance = pool,
                Implementation = ImplementationType.Singleton
            };

            Items.Add(typeof(TService), descriptor);
        }

        /// <summary>
        /// Adds a service pool to the container
        /// </summary>
        /// <param name="type">Implementation type</param>
        /// <param name="options">Options function</param>
        /// <param name="instance">After each instance is created, to do custom initialization, this method will be called.</param>
        private void AddPool<TService, TImplementation, TProxy>(ImplementationType type, Action<ServicePoolOptions> options, Action<TService> instance)
            where TService : class
            where TImplementation : class, TService
            where TProxy : class, IServiceProxy
        {
            ServicePool<TService, TImplementation, TProxy> pool = new ServicePool<TService, TImplementation, TProxy>(type, this, options, instance);
            ServiceDescriptor descriptor = new ServiceDescriptor
            {
                IsPool = true,
                ServiceType = typeof(TService),
                ProxyType = typeof(TProxy),
                ImplementationType = typeof(ServicePool<TService, TImplementation>),
                Instance = pool,
                Implementation = ImplementationType.Singleton
            };

            Items.Add(typeof(TService), descriptor);
        }

        #endregion

        #region Get

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public async Task<TService> Get<TService>(IContainerScope scope = null)
            where TService : class
        {
            object o = await Get(typeof(TService), scope);
            if (o == null)
                throw new NullReferenceException("Could not get service from container");

            return (TService)o;
        }

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public async Task<object> Get(Type serviceType, IContainerScope scope = null)
        {
            if (serviceType == typeof(IServiceContainer)) return this;

            ServiceDescriptor descriptor = GetDescriptor(serviceType);
            if (descriptor == null)
                throw new KeyNotFoundException($"Service type is not found: {serviceType.Name}");

            return await Get(descriptor, scope);
        }

        /// <summary>
        /// Try gets the service from the container.
        /// </summary>
        public Task<bool> TryGet<TService>(out TService service, IContainerScope scope = null) where TService : class
        {
            bool result = TryGet(typeof(TService), out object o, scope).Result;
            service = (TService)o;
            return Task.FromResult(result);
        }

        /// <summary>
        /// Try gets the service from the container.
        /// </summary>
        public Task<bool> TryGet(Type serviceType, out object service, IContainerScope scope = null)
        {
            if (serviceType == typeof(IServiceContainer))
            {
                service = this;
                return Task.FromResult(true);
            }

            ServiceDescriptor descriptor = GetDescriptor(serviceType);
            if (descriptor == null)
            {
                service = null;
                return Task.FromResult(false);
            }

            service = Get(descriptor, scope).Result;
            return Task.FromResult(true);
        }

        private async Task<object> Get(ServiceDescriptor descriptor, IContainerScope scope = null)
        {
            if (descriptor.IsPool)
            {
                IServicePool pool = (IServicePool)descriptor.Instance;
                PoolServiceDescriptor pdesc = await pool.GetAndLock(scope);

                if (pdesc == null)
                    throw new NullReferenceException("Could not get service from container");

                if (pool.Type == ImplementationType.Scoped && scope == null)
                    throw new InvalidOperationException("Type is registered as Scoped but scope parameter is null for IServiceContainer.Get method");

                if (scope != null)
                    scope.UsePoolItem(pool, pdesc);

                return pdesc.GetInstance();
            }

            switch (descriptor.Implementation)
            {
                //create new instance
                case ImplementationType.Transient:
                    object transient = await CreateInstance(descriptor.ImplementationType, scope);

                    if (descriptor.AfterCreatedMethod != null)
                        descriptor.AfterCreatedMethod.DynamicInvoke(transient);

                    if (descriptor.ProxyType != null)
                    {
                        IServiceProxy p = (IServiceProxy)await CreateInstance(descriptor.ProxyType, scope);
                        return p.Proxy(transient);
                    }

                    return transient;

                case ImplementationType.Scoped:

                    if (scope == null)
                        throw new InvalidOperationException("Type is registered as Scoped but scope parameter is null for IServiceContainer.Get method");

                    return await scope.Get(descriptor, this);

                case ImplementationType.Singleton:
                    //if instance already created return
                    if (descriptor.Instance != null)
                        return descriptor.Instance;

                    //create instance for first time and set Instance property of descriptor to prevent re-create for next times
                    object instance = await CreateInstance(descriptor.ImplementationType, scope);

                    if (descriptor.AfterCreatedMethod != null)
                        descriptor.AfterCreatedMethod.DynamicInvoke(instance);

                    if (descriptor.ProxyType != null)
                    {
                        IServiceProxy p = (IServiceProxy)await CreateInstance(descriptor.ProxyType, scope);
                        object proxyObject = p.Proxy(instance);
                        descriptor.Instance = proxyObject;
                    }
                    else
                        descriptor.Instance = instance;

                    return descriptor.Instance;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets descriptor of type
        /// </summary>
        public ServiceDescriptor GetDescriptor<TService>()
        {
            return GetDescriptor(typeof(TService));
        }

        /// <summary>
        /// Gets descriptor of type
        /// </summary>
        public ServiceDescriptor GetDescriptor(Type serviceType)
        {
            ServiceDescriptor descriptor;

            //finds by service type
            if (Items.ContainsKey(serviceType))
                descriptor = Items[serviceType];

            //if could not find by service type, tries to find by implementation type
            else
                descriptor = Items.Values.FirstOrDefault(x => x.ImplementationType == serviceType);

            return descriptor;
        }

        /// <summary>
        /// Creates instance of type.
        /// If it has constructor parameters, finds these parameters from the container
        /// </summary>
        public async Task<object> CreateInstance(Type type, IContainerScope scope = null)
        {
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
                throw new InvalidOperationException("There is no accessible constructor found in " + type.FullName);

            ConstructorInfo constructor = ctors[0];
            ParameterInfo[] parameters = constructor.GetParameters();

            //if parameterless create directly and return
            if (parameters.Length == 0)
                return Activator.CreateInstance(type);

            object[] values = new object[parameters.Length];

            //find all parameters from the container
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (typeof(IContainerScope).IsAssignableFrom(parameter.ParameterType))
                    values[i] = scope;
                else
                {
                    object value = await Get(parameter.ParameterType, scope);
                    values[i] = value;
                }
            }

            //create with parameters found from the container
            return Activator.CreateInstance(type, values);
        }

        /// <summary>
        /// Releases item from pool's locked item list
        /// </summary>
        public void ReleasePoolItem<TService>(TService service)
        {
            ServiceDescriptor descriptor = GetDescriptor<TService>();

            IServicePool pool = (IServicePool)descriptor.Instance;
            pool.ReleaseInstance(service);
        }

        /// <summary>
        /// Creates new scope belong this container.
        /// </summary>
        public IContainerScope CreateScope()
        {
            return new DefaultContainerScope();
        }

        #endregion

        #region Remove

        /// <summary>
        /// Removes the service from the container
        /// </summary>
        public void Remove<TService>()
            where TService : class
        {
            Remove(typeof(TService));
        }

        /// <summary>
        /// Removes the service from the container
        /// </summary>
        public void Remove(Type type)
        {
            if (Items.ContainsKey(type))
                Items.Remove(type);
        }

        #endregion

        #region Helper
        /// <summary>
        /// Check service is in container.
        /// </summary>
        public bool Contains(Type serviceType)
        {
            return Items.ContainsKey(serviceType);
        }

        /// <summary>
        /// Check service is in container.
        /// </summary>
        public bool Contains<T>()
        {
            return Contains(typeof(T));
        }
        #endregion
    }
}