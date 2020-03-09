using System;
using System.Collections.Generic;
using System.Threading;

namespace Twino.Ioc.Pool
{
    /// <summary>
    /// Handles pool item idle statuses.
    /// If they are timed out, this handler removes them from the pool.
    /// </summary>
    internal class PoolIdleHandler<TService, TImplementation> : IDisposable
        where TService : class
        where TImplementation : class, TService
    {
        private readonly ServicePool<TService, TImplementation> _pool;

        private Timer _timer;

        public PoolIdleHandler(ServicePool<TService, TImplementation> pool)
        {
            _pool = pool;
        }

        public void Start()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            _timer = new Timer(s => Elapsed(), null, 10000, 10000);
        }

        private void Elapsed()
        {
            List<PoolServiceDescriptor<TService>> descriptors;
            lock (_pool.Descriptors)
                descriptors = new List<PoolServiceDescriptor<TService>>(_pool.Descriptors);

            List<PoolServiceDescriptor<TService>> remove = new List<PoolServiceDescriptor<TService>>();

            foreach (PoolServiceDescriptor<TService> descriptor in descriptors)
            {
                if (!descriptor.IdleTimeout.HasValue || descriptor.IdleTimeout.Value > DateTime.UtcNow)
                    continue;

                if (descriptor.Locked)
                    continue;

                remove.Add(descriptor);
            }

            if (remove.Count == 0)
                return;

            lock (_pool.Descriptors)
                _pool.Descriptors.RemoveAll(x => remove.Contains(x));
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}