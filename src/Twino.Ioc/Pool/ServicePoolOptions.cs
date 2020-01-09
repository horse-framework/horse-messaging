using System;

namespace Twino.Ioc.Pool
{
    /// <summary>
    /// Pool options for Twino IOC Service pool
    /// </summary>
    public class ServicePoolOptions
    {
        /// <summary>
        /// Maximum lock duration for each locked service instance
        /// </summary>
        public TimeSpan MaximumLockDuration { get; set; }

        /// <summary>
        /// If all instances are locked, maximum wait duration for waiting unlock
        /// </summary>
        public TimeSpan WaitAvailableDuration { get; set; }

        /// <summary>
        /// Maximum service count in pool
        /// </summary>
        public int PoolMaxSize { get; set; }

        /// <summary>
        /// If true, requester will wait for WaitAvailableDuration If service instance limit exceeded
        /// </summary>
        public bool ExceedLimitWhenWaitTimeout { get; set; }
    }
}