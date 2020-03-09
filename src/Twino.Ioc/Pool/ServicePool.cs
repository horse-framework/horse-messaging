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
    public class ServicePool<TService, TImplementation> : IServicePool, IDisposable
        where TService : class
        where TImplementation : class, TService
    {
        #region Properties

        /// <summary>
        /// Pool instance implementation type
        /// </summary>
        public ImplementationType Type { get; internal set; }

        /// <summary>
        /// Active instances
        /// </summary>
        internal List<PoolServiceDescriptor<TService>> Descriptors { get; } = new List<PoolServiceDescriptor<TService>>();

        /// <summary>
        /// Initializer function for new created instances in pool
        /// </summary>
        protected readonly Action<TService> _func;

        /// <summary>
        /// Pool options
        /// </summary>
        public ServicePoolOptions Options { get; internal set; }

        /// <summary>
        /// Services container of pool
        /// </summary>
        public IServiceContainer Container { get; }

        /// <summary>
        /// Idle handler for the pool
        /// </summary>
        private PoolIdleHandler<TService, TImplementation> _idleHandler;

        #endregion

        #region Init - Release

        /// <summary>
        /// Crates new service pool belong the container with options and after instance creation functions
        /// </summary>
        /// <param name="type">Implementation type</param>
        /// <param name="container">Parent container</param>
        /// <param name="ofunc">Options function</param>
        /// <param name="func">After each instance is created, to do custom initialization, this method will be called.</param>
        public ServicePool(ImplementationType type, IServiceContainer container, Action<ServicePoolOptions> ofunc, Action<TService> func)
        {
            Type = type;
            Container = container;
            _func = func;

            Options = new ServicePoolOptions();
            Options.PoolMaxSize = 128;
            Options.MaximumLockDuration = TimeSpan.FromSeconds(60);
            Options.ExceedLimitWhenWaitTimeout = false;
            Options.WaitAvailableDuration = TimeSpan.Zero;

            if (ofunc != null)
                ofunc(Options);

            if (Options.IdleTimeout > TimeSpan.Zero)
            {
                _idleHandler = new PoolIdleHandler<TService, TImplementation>(this);
                _idleHandler.Start();
            }
        }

        /// <summary>
        /// Releases pool item by instance
        /// </summary>
        /// <returns></returns>
        public void ReleaseInstance(object instance)
        {
            PoolServiceDescriptor descriptor;
            lock (Descriptors)
                descriptor = Descriptors.Find(x => x.Instance == instance);

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
        /// Disposes pool and releases all resources
        /// </summary>
        public void Dispose()
        {
            _idleHandler?.Dispose();
        }

        #endregion

        #region Get

        /// <summary>
        /// Get an item from pool and locks it to prevent multiple usage at same time.
        /// The item should be released with Release method.
        /// </summary>
        public async Task<PoolServiceDescriptor> GetAndLock(IContainerScope scope = null)
        {
            PoolServiceDescriptor<TService> descriptor = GetFromCreatedItem(scope);

            if (descriptor != null)
                return descriptor;

            //if there is no available instance and we have space in pool, create new
            int count;
            lock (Descriptors)
                count = Descriptors.Count;

            if (count < Options.PoolMaxSize)
                return await CreateNew(scope, true);

            //if there is no available instance and there is no space to create new
            TaskCompletionSource<PoolServiceDescriptor<TService>> completionSource = new TaskCompletionSource<PoolServiceDescriptor<TService>>(TaskCreationOptions.RunContinuationsAsynchronously);
            ThreadPool.UnsafeQueueUserWorkItem(async state =>
            {
                try
                {
                    await WaitForAvailable(scope, state);
                }
                catch (Exception e)
                {
                    completionSource.SetException(e);
                }
            }, completionSource, false);

            return await completionSource.Task;
        }

        /// <summary>
        /// Waits until an item is available.
        /// If any available item cannot be found, creates new if exceed possible. Otherwise returns null
        /// </summary>
        private async Task WaitForAvailable(IContainerScope scope, TaskCompletionSource<PoolServiceDescriptor<TService>> state)
        {
            //try to get when available
            if (Options.WaitAvailableDuration > TimeSpan.Zero)
            {
                DateTime waitMax = DateTime.UtcNow.Add(Options.WaitAvailableDuration);
                while (DateTime.UtcNow < waitMax)
                {
                    await Task.Delay(5);
                    PoolServiceDescriptor<TService> pdesc = GetFromCreatedItem(scope);

                    if (pdesc != null)
                    {
                        state.SetResult(pdesc);
                        return;
                    }
                }
            }

            //tried to get but timed out, if we can exceed limit, create new one and return
            PoolServiceDescriptor<TService> result = Options.ExceedLimitWhenWaitTimeout ? (await CreateNew(scope, true)) : null;
            state.SetResult(result);
        }

        /// <summary>
        /// Gets service descriptor for re-use from already created services list
        /// </summary>
        private PoolServiceDescriptor<TService> GetFromCreatedItem(IContainerScope scope)
        {
            lock (Descriptors)
            {
                if (Type == ImplementationType.Scoped)
                {
                    PoolServiceDescriptor<TService> scoped = Descriptors.FirstOrDefault(x => x.Scope == scope);

                    if (scoped != null)
                    {
                        if (Options.IdleTimeout > TimeSpan.Zero)
                            scoped.IdleTimeout = DateTime.UtcNow + Options.IdleTimeout;
                        else
                            scoped.IdleTimeout = null;
                    }

                    return scoped;
                }

                PoolServiceDescriptor<TService> transient = Descriptors.FirstOrDefault(x => !x.Locked || x.LockExpiration < DateTime.UtcNow);
                if (transient == null)
                    return null;

                transient.Scope = scope;
                transient.Locked = true;
                transient.LockExpiration = DateTime.UtcNow.Add(Options.MaximumLockDuration);

                if (Options.IdleTimeout > TimeSpan.Zero)
                    transient.IdleTimeout = DateTime.UtcNow + Options.IdleTimeout;
                else
                    transient.IdleTimeout = null;

                return transient;
            }
        }

        /// <summary>
        /// Creates new instance and adds to pool
        /// </summary>
        protected virtual async Task<PoolServiceDescriptor<TService>> CreateNew(IContainerScope scope, bool locked)
        {
            PoolServiceDescriptor<TService> descriptor = new PoolServiceDescriptor<TService>();
            descriptor.Locked = locked;
            descriptor.Scope = scope;
            descriptor.LockExpiration = DateTime.UtcNow.Add(Options.MaximumLockDuration);
            if (Options.IdleTimeout > TimeSpan.Zero)
                descriptor.IdleTimeout = DateTime.UtcNow + Options.IdleTimeout;
            else
                descriptor.IdleTimeout = null;

            if (Type == ImplementationType.Scoped && scope != null)
            {
                //we couldn't find any created instance. create new.
                object instance = await Container.CreateInstance(typeof(TImplementation), scope);
                scope.PutItem(typeof(TService), instance);
                descriptor.Instance = (TService) instance;
            }
            else
            {
                object instance = await Container.CreateInstance(typeof(TImplementation), scope);
                descriptor.Instance = (TService) instance;
            }

            if (_func != null)
                _func(descriptor.Instance);

            lock (Descriptors)
                Descriptors.Add(descriptor);

            return descriptor;
        }

        #endregion
    }

    /// <summary>
    /// IOC Pool container.
    /// Contains same service instances in the pool.
    /// Provides available instances to requesters and guarantees that only requester uses same instances at same time
    /// </summary>
    public class ServicePool<TService, TImplementation, TProxy> : ServicePool<TService, TImplementation>
        where TService : class
        where TImplementation : class, TService
        where TProxy : class, IServiceProxy
    {
        /// <summary>
        /// Crates new service pool belong the container with options and after instance creation functions
        /// </summary>
        /// <param name="type">Implementation type</param>
        /// <param name="container">Parent container</param>
        /// <param name="ofunc">Options function</param>
        /// <param name="func">After each instance is created, to do custom initialization, this method will be called.</param>
        public ServicePool(ImplementationType type, IServiceContainer container, Action<ServicePoolOptions> ofunc, Action<TService> func)
            : base(type, container, ofunc, func)
        {
        }

        /// <summary>
        /// Creates new instance and adds to pool
        /// </summary>
        protected override async Task<PoolServiceDescriptor<TService>> CreateNew(IContainerScope scope, bool locked)
        {
            PoolServiceDescriptor<TService> descriptor = new PoolServiceDescriptor<TService>();
            descriptor.Locked = locked;
            descriptor.Scope = scope;
            descriptor.LockExpiration = DateTime.UtcNow.Add(Options.MaximumLockDuration);

            if (Options.IdleTimeout > TimeSpan.Zero)
                descriptor.IdleTimeout = DateTime.UtcNow + Options.IdleTimeout;
            else
                descriptor.IdleTimeout = null;

            if (Type == ImplementationType.Scoped && scope != null)
            {
                //we couldn't find any created instance. create new.
                object instance = await Container.CreateInstance(typeof(TImplementation), scope);
                IServiceProxy p = (IServiceProxy) await Container.CreateInstance(typeof(TProxy), scope);
                object proxyObj = p.Proxy(instance);
                scope.PutItem(typeof(TService), proxyObj);
                descriptor.Instance = (TService) proxyObj;
            }
            else
            {
                object instance = await Container.CreateInstance(typeof(TImplementation), scope);
                IServiceProxy p = (IServiceProxy) await Container.CreateInstance(typeof(TProxy), scope);
                object proxyObj = p.Proxy(instance);
                descriptor.Instance = (TService) proxyObj;
            }

            if (_func != null)
                _func(descriptor.Instance);

            lock (Descriptors)
                Descriptors.Add(descriptor);

            return descriptor;
        }
    }
}