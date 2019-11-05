using System;

namespace Twino.Ioc.Pool
{
    public class ServicePoolOptions
    {
        public TimeSpan MaximumLockDuration { get; set; }
        public TimeSpan WaitAvailableDuration { get; set; }
        
        public int PoolMaxSize { get; set; }
        
        public bool ExceedLimitWhenWaitTimeout { get; set; }
    }
}