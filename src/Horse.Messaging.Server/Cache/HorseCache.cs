using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Cache
{
    /// <summary>
    /// Horse cache manager
    /// </summary>
    public class HorseCache
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

        /// <summary>
        /// Event Manager for HorseEventType.CacheGet
        /// </summary>
        public EventManager GetEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.CacheSet
        /// </summary>
        public EventManager SetEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.CacheRemove
        /// </summary>
        public EventManager RemoveEvent { get; }

        /// <summary>
        /// Event Manager for HorseEventType.CachePurge
        /// </summary>
        public EventManager PurgeEvent { get; }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates new horse cacha manager
        /// </summary>
        public HorseCache(HorseRider rider)
        {
            Rider = rider;
            GetEvent = new EventManager(rider, HorseEventType.CacheGet);
            SetEvent = new EventManager(rider, HorseEventType.CacheSet);
            RemoveEvent = new EventManager(rider, HorseEventType.CacheRemove);
            PurgeEvent = new EventManager(rider, HorseEventType.CachePurge);
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
                _timer = new Timer(o => RemoveExpiredCacheItems(), null, 300000, 120000);
            }
        }

        #endregion

        #region Actions

        /// <summary>
        /// Gets all cache keys
        /// </summary>
        /// <returns></returns>
        public List<CacheInformation> GetCacheKeys()
        {
            List<CacheInformation> list;

            lock (_items)
            {
                list = new List<CacheInformation>(_items.Count);
                foreach (HorseCacheItem item in _items.Values)
                {
                    list.Add(new CacheInformation
                    {
                        Key = item.Key,
                        Expiration = item.Expiration.ToUnixMilliseconds(),
                        WarningDate = item.ExpirationWarning?.ToUnixMilliseconds() ?? 0,
                        WarnCount = item.ExpirationWarnCount
                    });
                }
            }

            return list;
        }

        /// <summary>
        /// Adds or sets a cache
        /// </summary>
        public CacheOperation Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null)
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
                Tags = tags ?? Array.Empty<string>(),
                Expiration = DateTime.UtcNow + d,
                ExpirationWarning = expirationWarning.HasValue
                    ? DateTime.UtcNow + expirationWarning.Value
                    : Options.ExpirationWarningIsEnabled
                        ? DateTime.UtcNow + Options.DefaultExpirationWarning
                        : DateTime.UtcNow + d,
                Value = new MemoryStream(value.ToArray())
            };

            lock (_items)
                _items[key] = item;

            return new CacheOperation(CacheResult.Ok, item);
        }

        /// <summary>
        /// Gets a cache from key
        /// </summary>
        public HorseCacheItem Get(string key, out bool firstWarningReceiver)
        {
            HorseCacheItem item;
            lock (_items)
                _items.TryGetValue(key, out item);

            if (item == null || item.Expiration < DateTime.UtcNow)
            {
                firstWarningReceiver = false;
                return null;
            }

            if (item.ExpirationWarning.HasValue && item.ExpirationWarning.Value < DateTime.UtcNow)
                lock (item)
                {
                    firstWarningReceiver = item.ExpirationWarnCount == 0;
                    item.ExpirationWarnCount++;
                }
            else
                firstWarningReceiver = false;

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
        /// Purges all keys with specified tagName
        /// </summary>
        public void PurgeByTag(string tagName)
        {
            List<string> keys = new List<string>(8);
            lock (_items)
            {
                foreach (KeyValuePair<string, HorseCacheItem> pair in _items)
                {
                    if (pair.Value.Tags.Contains(tagName, StringComparer.CurrentCultureIgnoreCase))
                        keys.Add(pair.Key);
                }

                foreach (var key in keys)
                    keys.Remove(key);
            }
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