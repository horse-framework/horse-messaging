using System;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Horse cache options
    /// </summary>
    public class HorseCacheOptions
    {
        /// <summary>
        /// Maximum cache duration
        /// </summary>
        public TimeSpan MaximumDuration { get; set; }

        /// <summary>
        /// Minimum cache duration
        /// </summary>
        public TimeSpan MinimumDuration { get; set; }

        /// <summary>
        /// default cache duration.
        /// Used if setter client does not speficy the duration.
        /// Default value is 30 minutes. 
        /// </summary>
        public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(30);
        
        /// <summary>
        /// Maximum cache key count
        /// </summary>
        public int MaximumKeys { get; set; }

        /// <summary>
        /// Maximum key length
        /// </summary>
        public int KeyMaxSize { get; set; }

        /// <summary>
        /// Maximum value size in bytes
        /// </summary>
        public int ValueMaxSize { get; set; }
    }
}