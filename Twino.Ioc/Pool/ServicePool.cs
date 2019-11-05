using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.Ioc.Pool
{
    /// <summary>
    /// IOC Pool container.
    /// Contains same service instances in the pool.
    /// Provides available instances to requesters and guarantees that only requester uses same instances at same time
    /// </summary>
    public class ServicePool<TService> : IServicePool
        where TService : class
    {
        /// <summary>
        /// Active instances
        /// </summary>
        private readonly List<PoolServiceDescriptor<TService>> _descriptors = new List<PoolServiceDescriptor<TService>>();

        /// <summary>
        /// Initializer function for new created instances in pool
        /// </summary>
        private readonly Action<TService> _func;

        /// <summary>
        /// Pool options
        /// </summary>
        public ServicePoolOptions Options { get; internal set; }

        /// <summary>
        /// Services container of pool
        /// </summary>
        public IServiceContainer Container { get; }

        /// <summary>
        /// Crates new service pool belong the container with options and after instance creation functions
        /// </summary>
        /// <param name="container">Parent container</param>
        /// <param name="ofunc">Options function</param>
        /// <param name="func">After each instance is created, to do custom initialization, this method will be called.</param>
        public ServicePool(IServiceContainer container, Action<ServicePoolOptions> ofunc, Action<TService> func)
        {
            Container = container;
            _func = func;

            Options = new ServicePoolOptions();
            ofunc(Options);

            container.AddTransient<TService, TService>();
        }

        /// <summary>
        /// Releases pool item by instance
        /// </summary>
        /// <returns></returns>
        public void ReleaseInstance(object instance)
        {
            PoolServiceDescriptor descriptor;
            lock (_descriptors)
                descriptor = _descriptors.Find(x => x.Instance == instance);

            if (descriptor != null)
                Release(descriptor);
        }

        /// <summary>
        /// Releases pool item for re-using
        /// </summary>
        public void Release(PoolServiceDescriptor descriptor)
        {
            descriptor.Locked = false;
        }

        /// <summary>
        /// Get an item from pool and locks it to prevent multiple usage at same time.
        /// The item should be released with Release method.
        /// </summary>
        public async Task<PoolServiceDescriptor> GetAndLock()
        {
            PoolServiceDescriptor<TService> descriptor = GetFromCreatedItem();

            if (descriptor != null)
                return descriptor;

            //if there is no available instance and we have space in pool, create new
            int count;
            lock (_descriptors)
                count = _descriptors.Count;

            if (count < Options.PoolMaxSize)
                return await CreateNew(true);


            //if there is no available instance and there is no space to create new
            TaskCompletionSource<PoolServiceDescriptor<TService>> completionSource = new TaskCompletionSource<PoolServiceDescriptor<TService>>(TaskCreationOptions.None);
            ThreadPool.UnsafeQueueUserWorkItem(async state => await WaitForAvailable(state), completionSource, false);

            return await completionSource.Task;
        }

        /// <summary>
        /// Waits until an item is available.
        /// If any available item cannot be found, creates new if exceed possible. Otherwise returns null
        /// </summary>
        private async Task WaitForAvailable(TaskCompletionSource<PoolServiceDescriptor<TService>> state)
        {
            //try to get when available
            DateTime waitMax = DateTime.UtcNow.Add(Options.WaitAvailableDuration);
            while (DateTime.UtcNow < waitMax)
            {
                await Task.Delay(5);
                PoolServiceDescriptor<TService> pdesc = GetFromCreatedItem();
                if (pdesc != null)
                {
                    state.SetResult(pdesc);
                    break;
                }
            }

            //tried to get but timed out, if we can exceed limit, create new one and return
            PoolServiceDescriptor<TService> result = Options.ExceedLimitWhenWaitTimeout ? (await CreateNew(true)) : null;
            state.SetResult(result);
        }

        /// <summary>
        /// Gets service descriptor for re-use from already created services list
        /// </summary>
        private PoolServiceDescriptor<TService> GetFromCreatedItem()
        {
            lock (_descriptors)
            {
                PoolServiceDescriptor<TService> descriptor = _descriptors.FirstOrDefault(x => !x.Locked || x.LockExpiration < DateTime.UtcNow);

                if (descriptor != null)
                {
                    descriptor.Locked = true;
                    descriptor.LockExpiration = DateTime.UtcNow.Add(Options.MaximumLockDuration);
                    return descriptor;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates new instance and adds to pool
        /// </summary>
        private async Task<PoolServiceDescriptor<TService>> CreateNew(bool locked)
        {
            PoolServiceDescriptor<TService> descriptor = new PoolServiceDescriptor<TService>();
            descriptor.Locked = locked;
            descriptor.LockExpiration = DateTime.UtcNow.Add(Options.MaximumLockDuration);

            object instance = await Container.CreateInstance(typeof(TService));
            descriptor.Instance = (TService) instance;

            if (_func != null)
                _func(descriptor.Instance);

            lock (_descriptors)
                _descriptors.Add(descriptor);

            return descriptor;
        }
    }
}