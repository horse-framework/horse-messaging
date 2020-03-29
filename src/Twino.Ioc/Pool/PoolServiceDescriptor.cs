using System;

namespace Twino.Ioc.Pool
{
    /// <summary>
    /// Service instance in a pool descriptor
    /// </summary>
    public class PoolServiceDescriptor<TService> : PoolServiceDescriptor
        where TService : class
    {
        /// <summary>
        /// Service instance
        /// </summary>
        public TService Instance { get; internal set; }
        
        /// <summary>
        /// Gets instance of the service
        /// </summary>
        /// <returns></returns>
        public override object GetInstance()
        {
            return Instance;
        }
    }

    /// <summary>
    /// Service instance in a pool descriptor
    /// </summary>
    public abstract class PoolServiceDescriptor
    {
        /// <summary>
        /// Expiration time for locking the instance
        /// </summary>
        public DateTime LockExpiration { get; internal set; }

        /// <summary>
        /// True, if service instance is locked by a scope
        /// </summary>
        public bool Locked { get; internal set; }

        /// <summary>
        /// Serviece instance locker scope
        /// </summary>
        public IContainerScope Scope { get; internal set; }

        /// <summary>
        /// Deadline for the instance.
        /// If the instance is not used by the pool until, it will be destroyed.
        /// If null, there is no timeout
        /// </summary>
        public DateTime? IdleTimeout { get; internal set; }

        /// <summary>
        /// Gets instance of the service
        /// </summary>
        public abstract object GetInstance();
    }
}