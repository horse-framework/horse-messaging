using System.Threading.Tasks;

namespace Twino.Ioc.Pool
{
    public interface IServicePool
    {
        /// <summary>
        /// Gets and locks a service instance.
        /// Return type should be PoolServiceDescriptor<TService> 
        /// </summary>
        /// <returns></returns>
        Task<PoolServiceDescriptor> GetAndLock();

        /// <summary>
        /// Releases pool item for re-using. 
        /// </summary>
        void Release(PoolServiceDescriptor descriptor);
    }
}