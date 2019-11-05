using System;

namespace Twino.Ioc.Pool
{
    public class PoolServiceDescriptor<TService> : PoolServiceDescriptor
        where TService : class
    {
        public TService Instance { get; internal set; }

        public override object GetInstance()
        {
            return Instance;
        }
    }

    public abstract class PoolServiceDescriptor
    {
        public DateTime LockExpiration { get; internal set; }
        public bool Locked { get; internal set; }

        public abstract object GetInstance();
    }
}