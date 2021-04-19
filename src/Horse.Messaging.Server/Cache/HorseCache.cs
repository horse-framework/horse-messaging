using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Horse cache manager
    /// </summary>
    public class HorseCache : IHorseCache
    {
        #region Properties

        private Timer _timer;
        private bool _initialized;
        private readonly SortedDictionary<string, HorseCacheItem> _items = new SortedDictionary<string, HorseCacheItem>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Cache authorizations
        /// </summary>
        public ArrayContainer<ICacheAuthorization> Authorizations { get; } = new ArrayContainer<ICacheAuthorization>();

        /// <summary>
        /// Options for cache
        /// </summary>
        public HorseCacheOptions Options { get; } = new HorseCacheOptions();

        /// <summary>
        /// Root horse rider object
        /// </summary>
        public HorseRider Rider { get; }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates new horse cacha manager
        /// </summary>
        public HorseCache(HorseRider rider)
        {
            Rider = rider;
        }

        /// <summary>
        /// Initializes horse cache
        /// </summary>
        public void Initialize()
        {
            lock (this)
            {
                if (_initialized)
                    return;

                _initialized = true;
                _timer = new Timer(o => RemoveExpiredCacheItems(), null, 60000, 60000);
            }
        }

        #endregion

        #region Actions

        /// <summary>
        /// Adds or sets a cache
        /// </summary>
        public CacheOperation Set(string key, MemoryStream value, TimeSpan duration)
        {
            if (Options.MaximumKeys > 0 && _items.Count >= Options.MaximumKeys)
                return new CacheOperation(CacheResult.KeyLimit, null);

            if (Options.ValueMaxSize > 0 && value.Length > Options.ValueMaxSize)
                return new CacheOperation(CacheResult.ItemSizeLimit, null);

            TimeSpan d = duration;
            if (d == TimeSpan.Zero)
                d = Options.DefaultDuration;

            if (Options.MinimumDuration > TimeSpan.Zero && d < Options.MinimumDuration)
                d = Options.MinimumDuration;
            else if (Options.MaximumDuration > TimeSpan.Zero && d > Options.MaximumDuration)
                d = Options.MaximumDuration;

            HorseCacheItem item = new HorseCacheItem
            {
                Key = key,
                Expiration = DateTime.UtcNow + d,
                Value = new MemoryStream(value.ToArray())
            };

            lock (_items)
            {
                if (_items.ContainsKey(key))
                    _items[key] = item;
                else
                    _items.Add(key, item);
            }

            return new CacheOperation(CacheResult.Ok, item);
        }

        /// <summary>
        /// Gets a cache from key
        /// </summary>
        public HorseCacheItem Get(string key)
        {
            HorseCacheItem item;
            lock (_items)
            {
                _items.TryGetValue(key, out item);
            }

            if (item == null || item.Expiration < DateTime.UtcNow)
                return null;

            return item;
        }

        /// <summary>
        /// Removes a key
        /// </summary>
        public void Remove(string key)
        {
            lock (_items)
                _items.Remove(key);
        }

        /// <summary>
        /// Purges all keys
        /// </summary>
        public void Purge()
        {
            lock (_items)
                _items.Clear();
        }

        /// <summary>
        /// Removes expired cache items
        /// </summary>
        private void RemoveExpiredCacheItems()
        {
            List<string> keys = new List<string>();

            lock (_items)
            {
                foreach (KeyValuePair<string, HorseCacheItem> item in _items)
                {
                    if (item.Value.Expiration < DateTime.UtcNow)
                        keys.Add(item.Key);
                }

                foreach (string key in keys)
                    _items.Remove(key);
            }
        }

        #endregion
    }
}