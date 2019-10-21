using System;

namespace Twino.Mvc.Services
{
    /// <summary>
    /// Service creation and keep types
    /// </summary>
    public enum ImplementationType
    {
        /// <summary>
        /// For each call, new instance is created
        /// </summary>
        Instance,

        /// <summary>
        /// Instance is created only once, returns same object for each call
        /// </summary>
        Singleton
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
        /// Implementation method
        /// </summary>
        public ImplementationType Implementation { get; set; }
    }

}
