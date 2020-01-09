using System.Threading.Tasks;

namespace Twino.Ioc.Pool
{
    /// <summary>
    /// Service pool implementation, contains multiple services with same type
    /// </summary>
    public interface IServicePool
    {
        /// <summary>
        /// Pool instance implementation type
        /// </summary>
        ImplementationType Type { get; }
        
        /// <summary>
        /// Gets and locks a service instance.
        /// Return type should be PoolServiceDescriptor with generic TService template 
        /// </summary>
        /// <returns></returns>
        Task<PoolServiceDescriptor> GetAndLock(IContainerScope scope = null);

        /// <summary>
        /// Releases pool item by instance
        /// </summary>
        /// <returns></returns>
        void ReleaseInstance(object instance);

        /// <summary>
        /// Releases pool item for re-using. 
        /// </summary>
        void Release(PoolServiceDescriptor descriptor);
    }
}