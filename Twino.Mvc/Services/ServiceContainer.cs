using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Twino.Mvc.Services
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

        public ServiceContainer()
        {
            Items = new Dictionary<Type, ServiceDescriptor>();
        }

        #region Add

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
        public void AddTransient(Type serviceType, Type implementationType)
        {
            if (Items.ContainsKey(serviceType))
                throw new InvalidOperationException("Specified service type is already added into service container");

            ServiceDescriptor descriptor = new ServiceDescriptor
                                           {
                                               ServiceType = serviceType,
                                               ImplementationType = implementationType,
                                               Instance = null,
                                               Implementation = ImplementationType.Transient
                                           };

            Items.Add(serviceType, descriptor);
        }

        
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
        public void AddScoped(Type serviceType, Type implementationType)
        {
            if (Items.ContainsKey(serviceType))
                throw new InvalidOperationException("Specified service type is already added into service container");

            ServiceDescriptor descriptor = new ServiceDescriptor
                                           {
                                               ServiceType = serviceType,
                                               ImplementationType = implementationType,
                                               Instance = null,
                                               Implementation = ImplementationType.Scoped
                                           };

            Items.Add(serviceType, descriptor);
        }

        
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
        /// Adds a singleton service with instance to the container.
        /// </summary>
        public void AddSingleton<TService, TImplementation>(TImplementation instance)
            where TService : class
            where TImplementation : class, TService
        {
            AddSingleton(typeof(TService), instance);
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

        #region Get

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public TService Get<TService>(IContainerScope scope = null)
            where TService : class
        {
            return (TService) Get(typeof(TService), scope);
        }

        /// <summary>
        /// Gets the service from the container.
        /// </summary>
        public object Get(Type serviceType, IContainerScope scope = null)
        {
            ServiceDescriptor descriptor = GetDescriptor(serviceType);

            if (descriptor.Implementation == ImplementationType.Scoped)
            {
                if (scope == null)
                    throw new InvalidOperationException("Type is registered as Scoped but scope parameter is null for IServiceContainer.Get method");

                return scope.Get(serviceType, this);
            }

            //for singleton
            if (descriptor.Implementation == ImplementationType.Singleton)
            {
                //if instance already created return
                if (descriptor.Instance != null)
                    return descriptor.Instance;

                //create instance for first time and set Instance property of descriptor to prevent re-create for next times
                object instance = CreateInstance(descriptor.ImplementationType, scope);
                descriptor.Instance = instance;

                return instance;
            }

            //create instance non-singleton
            return CreateInstance(descriptor.ImplementationType, scope);
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
            ServiceDescriptor descriptor = null;

            //finds by service type
            if (Items.ContainsKey(serviceType))
                descriptor = Items[serviceType];

            //if could not find by service type, tries to find by implementation type
            else
                descriptor = Items.Values.FirstOrDefault(x => x.ImplementationType == serviceType);

            if (descriptor == null)
                throw new KeyNotFoundException("Service type is not found");

            return descriptor;
        }

        /// <summary>
        /// Creates instance of type.
        /// If it has constructor parameters, finds these parameters from the container
        /// </summary>
        public object CreateInstance(Type type, IContainerScope scope = null)
        {
            ConstructorInfo constructor = type.GetConstructors()[0];
            ParameterInfo[] parameters = constructor.GetParameters();

            //if parameterless create directly and return
            if (parameters.Length == 0)
                return Activator.CreateInstance(type);

            object[] values = new object[parameters.Length];

            //find all parameters from the container
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                object value = Get(parameter.ParameterType, scope);
                values[i] = value;
            }

            //create with parameters found from the container
            return Activator.CreateInstance(type, values);
        }

        /// <summary>
        /// Creates new scope belong this container.
        /// </summary>
        public IContainerScope CreateScope()
        {
            return new RequestContainerScope();
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
    }
}