using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Twino.Extensions.Http
{
    /// <summary>
    /// Handles and manages HttpClient instances
    /// </summary>
    public class HttpClientFactory : IHttpClientFactory
    {
        /// <summary>
        /// All descriptors for specified names and "default" name
        /// </summary>
        private readonly Dictionary<string, HttpClientPoolDescriptor> _descriptors = new Dictionary<string, HttpClientPoolDescriptor>();

        /// <summary>
        /// Maximum lock duration for each instance
        /// </summary>
        private readonly TimeSpan PoolItemExpiration = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Adds new names configuration
        /// </summary>
        internal void AddConfiguration(string name, int poolSize, Action<HttpClient> configureAction)
        {
            HttpClientPoolDescriptor descriptor = new HttpClientPoolDescriptor
            {
                Name = name,
                PoolSize = poolSize,
                OptionsAction = configureAction,
                Items = poolSize > 0 ? new PoolItem[poolSize] : null
            };

            _descriptors.Add(name, descriptor);
        }

        /// <summary>
        /// Creates new HttpClient instance from application default factory
        /// </summary>
        public HttpClient Create()
        {
            return Create("default");
        }

        /// <summary>
        /// Creates new HttpClient instance from specified factory
        /// </summary>
        public HttpClient Create(string name)
        {
            HttpClientPoolDescriptor descriptor = _descriptors[name];

            //if pooling, search in pool
            if (descriptor.PoolSize > 0)
            {
                for (int i = 0; i < descriptor.Items.Length; i++)
                {
                    PoolItem item = descriptor.Items[i];

                    //we are creating instances by index order.
                    //if the code reaches to null item, it means all items are locked.
                    //create new instance and add into the pool
                    if (item == null)
                    {
                        item = new PoolItem();
                        item.Locked = true;
                        item.Expiration = DateTime.UtcNow + PoolItemExpiration;
                        descriptor.Items[i] = item;
                        item.Init(descriptor);
                        return item.Instance;
                    }

                    //if we found an unlocked or lock expired item, re-use it.
                    if (!item.Locked || item.Expiration < DateTime.UtcNow)
                    {
                        //update lock
                        lock (item)
                        {
                            if (item.Locked)
                                continue;

                            item.Locked = true;
                            item.Expiration = DateTime.UtcNow + PoolItemExpiration;
                        }

                        //init if needed
                        item.Init(descriptor);

                        return item.Instance;
                    }
                }
            }

            //this code id executed when we are not using pool or pool is full.
            //for both cases, we need to create an independent instance and return
            HttpClient instance = new HttpClient();
            descriptor.OptionsAction?.Invoke(instance);

            return instance;
        }

        /// <summary>
        /// Releases the HttpClient instance from application default factory
        /// </summary>
        public void Release(HttpClient client)
        {
            Release("default", client);
        }

        /// <summary>
        /// Releases the HttpClient instance from specified factory
        /// </summary>
        public void Release(string name, HttpClient client)
        {
            HttpClientPoolDescriptor descriptor = _descriptors[name];
            if (descriptor.PoolSize < 1 || descriptor.Items == null || descriptor.Items.Length < 1)
                return;

            foreach (var item in descriptor.Items)
            {
                if (item != null && item.Instance == client)
                {
                    item.Locked = false;
                    return;
                }
            }
        }
    }
}