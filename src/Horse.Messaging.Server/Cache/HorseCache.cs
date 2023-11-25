using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Cache Item data
/// </summary>
/// <param name="IsFirstWarningReceiver">True, if first time received the value in warning time range</param>
/// <param name="item">Cache item</param>
public record GetCacheItemResult(bool IsFirstWarningReceiver, HorseCacheItem item);

/// <summary>
/// Horse cache manager
/// </summary>
public class HorseCache
{
    #region Properties

    /// <summary>
    /// Cache authorizations
    /// </summary>
    public ArrayContainer<ICacheAuthorization> Authorizations { get; } = new();

    /// <summary>
    /// Options for cache
    /// </summary>
    public HorseCacheOptions Options { get; } = new();

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

    /// <summary>
    /// Cluster notifier
    /// </summary>
    internal CacheClusterNotifier ClusterNotifier { get; }

    private Timer _timer;
    private bool _initialized;

    private readonly SortedDictionary<string, HorseCacheItem> _items = new(StringComparer.InvariantCultureIgnoreCase);

    private volatile bool _hasChanges;
    private readonly List<Tuple<bool, HorseCacheItem>> _changes = new(16);
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    #endregion

    #region Initialization

    /// <summary>
    /// Creates new horse cacha manager
    /// </summary>
    public HorseCache(HorseRider rider)
    {
        Rider = rider;
        ClusterNotifier = new CacheClusterNotifier(this, rider.Cluster);
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
            _timer = new Timer(o => _ = ApplyChanges(true), null, 300000, 120000);
        }
    }

    #endregion

    #region Actions

    private async Task ApplyChanges(bool removeExpiredKeys = false)
    {
        if (_hasChanges || removeExpiredKeys)
        {
            await _semaphore.WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                foreach (Tuple<bool, HorseCacheItem> tuple in _changes)
                {
                    if (tuple.Item1)
                        _items[tuple.Item2.Key] = tuple.Item2;
                    else
                    {
                        if (tuple.Item2 == null)
                            _items.Clear();
                        else
                            _items.Remove(tuple.Item2.Key);
                    }
                }

                if (removeExpiredKeys)
                {
                    List<string> keys = new List<string>();

                    foreach (KeyValuePair<string, HorseCacheItem> item in _items)
                    {
                        if (item.Value.Expiration < DateTime.UtcNow)
                            keys.Add(item.Key);
                    }

                    foreach (string key in keys)
                        _items.Remove(key);

                    await Task.Delay(10);
                }

                _hasChanges = false;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// Gets all cache keys
    /// </summary>
    /// <returns></returns>
    public async Task<List<CacheInformation>> GetCacheKeys()
    {
        await ApplyChanges();

        List<CacheInformation> list = new List<CacheInformation>(_items.Count);
        foreach (HorseCacheItem item in _items.Values)
        {
            list.Add(new CacheInformation
            {
                Key = item.Key,
                Expiration = item.Expiration.ToUnixSeconds(),
                WarningDate = item.ExpirationWarning?.ToUnixSeconds() ?? 0,
                WarnCount = item.ExpirationWarnCount,
                Tags = item.Tags ?? Array.Empty<string>()
            });

            if (item.Expiration < DateTime.UtcNow)
                await Remove(item.Key);
        }

        return list;
    }

    /// <summary>
    /// Adds or sets a cache
    /// </summary>
    public Task<CacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null)
    {
        return Set(null, true, key, value, duration, expirationWarning, tags);
    }

    /// <summary>
    /// Adds or sets a cache
    /// </summary>
    internal async Task<CacheOperation> Set(MessagingClient client, bool notifyCluster, string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null)
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

        await _semaphore.WaitAsync();
        try
        {
            _hasChanges = true;
            _changes.Add(new Tuple<bool, HorseCacheItem>(true, item));
        }
        finally
        {
            _semaphore.Release();
        }

        Rider.Cache.SetEvent.Trigger(client, key);
        if (notifyCluster)
            ClusterNotifier.SendSet(item);

        return new CacheOperation(CacheResult.Ok, item);
    }

    /// <summary>
    /// Gets a cache from key
    /// </summary>
    public async Task<GetCacheItemResult> Get(string key)
    {
        await ApplyChanges();

        _items.TryGetValue(key, out HorseCacheItem item);

        if (item == null)
            return null;

        if (item.Expiration < DateTime.UtcNow)
        {
            await Remove(item.Key);
            return null;
        }

        bool firstWarningReceiver;
        if (item.ExpirationWarning.HasValue && item.ExpirationWarning.Value < DateTime.UtcNow)
            lock (item)
            {
                firstWarningReceiver = item.ExpirationWarnCount == 0;
                item.ExpirationWarnCount++;
            }
        else
            firstWarningReceiver = false;

        return new GetCacheItemResult(firstWarningReceiver, item);
    }

    /// <summary>
    /// Gets an integer value, increases each time received.
    /// If there is no item with that key, it's created with value of 1.
    /// If there was an item with that key and it's expired, it's created with value of 1.
    /// </summary>
    public Task<GetCacheItemResult> GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null)
    {
        return GetIncremental(true, key, duration, incrementValue, tags);
    }

    /// <summary>
    /// Gets an integer value, increases each time received.
    /// If there is no item with that key, it's created with value of 1.
    /// If there was an item with that key and it's expired, it's created with value of 1.
    /// </summary>
    internal async Task<GetCacheItemResult> GetIncremental(bool notifyCluster, string key, TimeSpan duration, int incrementValue, string[] tags = null)
    {
        await ApplyChanges();

        _items.TryGetValue(key, out HorseCacheItem item);

        if (item.Expiration < DateTime.UtcNow)
        {
            await Remove(item.Key);
            item = null;
        }

        if (item == null)
        {
            CacheOperation operation = await Set(key, new MemoryStream(BitConverter.GetBytes(1)), duration, null, tags);
            return new GetCacheItemResult(false, operation.Item);
        }

        MemoryStream previousValue = item.Value;
        lock (item)
        {
            byte[] valueArray = item.Value.ToArray();
            int value = BitConverter.ToInt32(valueArray);
            item.Value = new MemoryStream(BitConverter.GetBytes(value + incrementValue));
        }

        previousValue?.Dispose();

        if (notifyCluster)
            ClusterNotifier.SendSet(item);

        return new GetCacheItemResult(false, item);
    }

    /// <summary>
    /// Removes a key
    /// </summary>
    public Task Remove(string key)
    {
        return Remove(null, key, true);
    }

    /// <summary>
    /// Removes a key
    /// </summary>
    internal async Task Remove(MessagingClient client, string key, bool notifyCluster)
    {
        await _semaphore.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            _hasChanges = true;
            _changes.Add(new Tuple<bool, HorseCacheItem>(false, new HorseCacheItem {Key = key}));
        }
        finally
        {
            _semaphore.Release();
        }

        Rider.Cache.RemoveEvent.Trigger(client, key);
            
        if (notifyCluster)
            ClusterNotifier.SendRemove(key);
    }

    /// <summary>
    /// Purges all keys with specified tagName
    /// </summary>
    public Task PurgeByTag(string tagName)
    {
        return PurgeByTag(tagName, null, true);
    }

    /// <summary>
    /// Purges all keys with specified tagName
    /// </summary>
    internal async Task PurgeByTag(string tagName, MessagingClient client, bool notifyCluster)
    {
        await _semaphore.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            _hasChanges = true;
            foreach (KeyValuePair<string, HorseCacheItem> pair in _items)
            {
                if (pair.Value.Tags.Contains(tagName, StringComparer.CurrentCultureIgnoreCase))
                    _changes.Add(new Tuple<bool, HorseCacheItem>(false, pair.Value));
            }
        }
        finally
        {
            _semaphore.Release();
        }

        Rider.Cache.PurgeEvent.Trigger(client);
            
        if (notifyCluster)
            ClusterNotifier.SendPurge(tagName);
    }

    /// <summary>
    /// Purges all keys
    /// </summary>
    public Task Purge()
    {
        return Purge(null, true);
    }

    /// <summary>
    /// Purges all keys
    /// </summary>
    internal async Task Purge(MessagingClient client, bool notifyCluster)
    {
        await _semaphore.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            _hasChanges = true;
            _changes.Add(new Tuple<bool, HorseCacheItem>(false, null));
        }
        finally
        {
            _semaphore.Release();
        }

        Rider.Cache.PurgeEvent.Trigger(client);
            
        if (notifyCluster)
            ClusterNotifier.SendPurge(null);
    }

    #endregion
}