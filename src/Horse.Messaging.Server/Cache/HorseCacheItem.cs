using System;
using System.IO;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Cache Item
    /// </summary>
    public class HorseCacheItem
    {
        /// <summary>
        /// Cache key
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Expiration date
        /// </summary>
        public DateTime Expiration { get; set; }

        /// <summary>
        /// Cache value
        /// </summary>
        public MemoryStream Value { get; set; }
    }
}