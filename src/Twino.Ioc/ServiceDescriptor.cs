using System;

namespace Twino.Ioc
{
    /// <summary>
    /// Service creation and keep types
    /// </summary>
    public enum ImplementationType
    {
        /// <summary>
        /// For each call, new instance is created
        /// </summary>
        Transient,

        /// <summary>
        /// Instance is created only once, returns same object for each call
        /// </summary>
        Singleton,

        /// <summary>
        /// Instance is created only once for per scope.
        /// For different scopes, different instances are created
        /// </summary>
        Scoped
    }

    /// <summary>
    /// Service definition description for the Dependency Inversion Container
    /// </summary>
    public class ServiceDescriptor
    {
        /// <summary>
        /// Service type.
        /// End-user will ask the implementation type with this type.
        /// Usually this is interface type
        /// </summary>
        public Type ServiceType { get; set; }

        /// <summary>
        /// Real object type.
        /// Usually end-user doesn't know this type.
        /// </summary>
        public Type ImplementationType { get; set; }

        /// <summary>
        /// If the descriptor type is Singleton, this object keeps the singleton object.
        /// </summary>
        public object Instance { get; set; }

        /// <summary>
        /// Decorator type.
        /// </summary>
        public Type ProxyType { get; set; }

        /// <summary>
        /// Decorator instance
        /// </summary>
        public IServiceProxy ProxyInstance { get; set; }

        /// <summary>
        /// Implementation method
        /// </summary>
        public ImplementationType Implementation { get; set; }

        /// <summary>
        /// If not null, called for creating instance of the object
        /// </summary>
        public Func<IServiceProvider, object> ImplementationFactory { get; set; }

        /// <summary>
        /// If true, implementation is pool. Instance is type of IServicePool with generic TService template
        /// </summary>
        public bool IsPool { get; set; }

        /// <summary>
        /// This method is called after instance is created
        /// </summary>
        public Delegate AfterCreatedMethod { get; set; }

        /// <summary>
        /// Reference descriptor for Microsoft Extensions implementation
        /// </summary>
        internal Microsoft.Extensions.DependencyInjection.ServiceDescriptor MicrosoftServiceDescriptor { get; set; }
    }
}