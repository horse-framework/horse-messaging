using System;
using System.Net.Http;

namespace Twino.Extensions.Http
{
    /// <summary>
    /// HttpClient descriptor for name based factory
    /// </summary>
    internal class HttpClientPoolDescriptor
    {
        /// <summary>
        /// Factory name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Factory pool size
        /// </summary>
        public int PoolSize { get; set; }

        /// <summary>
        /// After created action for options initialization
        /// </summary>
        public Action<HttpClient> OptionsAction { get; set; }

        /// <summary>
        /// Pool items
        /// </summary>
        public PoolItem[] Items { get; set; }
    }

    /// <summary>
    /// HttpClient Factory pool item 
    /// </summary>
    internal class PoolItem
    {
        /// <summary>
        /// True, If some process is using this item
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// Deadline for the process. This item will be given to another process if this value expires.
        /// </summary>
        public DateTime Expiration { get; set; }

        /// <summary>
        /// Initialized instance for the item. Only crated one time
        /// </summary>
        public HttpClient Instance { get; set; }

        /// <summary>
        /// Initialized the item when instance is being created for first time
        /// </summary>
        public void Init(HttpClientPoolDescriptor descriptor)
        {
            if (Instance != null)
                return;

            Instance = new HttpClient();

            if (descriptor.OptionsAction != null)
                descriptor.OptionsAction(Instance);
        }
    }
}