using System;
using System.Collections.Concurrent;
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
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Cache Item data
/// </summary>
/// <param name="IsFirstWarningReceiver">True, if first time received the value in warning time range</param>
/// <param name="Item">Cache item</param>
public record GetCacheItemResult(bool IsFirstWarningReceiver, HorseCacheItem Item);

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

    private readonly ConcurrentDictionary<string, HorseCacheItem> _items = new(StringComparer.InvariantCultureIgnoreCase);

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
            _timer = new Timer(o => _ = RemoveExpiredKeys(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            LoadPersistentItems();
        }
    }

    #endregion

    #region Actions

    private async Task RemoveExpiredKeys()
    {
        List<string> keys = new List<string>();

        foreach (KeyValuePair<string, HorseCacheItem> item in _items)
        {
            if (item.Value.Expiration < DateTime.UtcNow)
                keys.Add(item.Key);
        }

        foreach (string key in keys)
            _items.TryRemove(key, out _);
    }

    /// <summary>
    /// Gets all cache keys
    /// </summary>
    /// <returns></returns>
    public async Task<List<CacheInformation>> GetCacheKeys()
    {
        await RemoveExpiredKeys();

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
    public Task<CacheOperation> Set(string key, string value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        return Set(null, true, key, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value)), duration, expirationWarning, tags, persistent);
    }


    /// <summary>
    /// Adds or sets a cache
    /// </summary>
    public Task<CacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        return Set(null, true, key, value, duration, expirationWarning, tags, persistent);
    }

    /// <summary>
    /// Adds or sets a cache
    /// </summary>
    internal async Task<CacheOperation> Set(MessagingClient client, bool notifyCluster, string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
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
            Tags = tags ?? [],
            Expiration = DateTime.UtcNow + d,
            ExpirationWarning = expirationWarning.HasValue
                ? DateTime.UtcNow + expirationWarning.Value
                : Options.ExpirationWarningIsEnabled
                    ? DateTime.UtcNow + Options.DefaultExpirationWarning
                    : DateTime.UtcNow + d,
            Value = new MemoryStream(value.ToArray())
        };

        _items[key] = item;

        Rider.Cache.SetEvent.Trigger(client, key);
        if (notifyCluster)
            ClusterNotifier.SendSet(item);

        if (persistent)
        {
            item.IsPersistent = true;
            WritePersistentItem(item);
        }

        return new CacheOperation(CacheResult.Ok, item);
    }

    /// <summary>
    /// Gets a cache from key
    /// </summary>
    public async Task<GetCacheItemResult> Get(string key)
    {
        await RemoveExpiredKeys();

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
        await RemoveExpiredKeys();

        _items.TryGetValue(key, out HorseCacheItem item);
        if (item == null)
        {
            CacheOperation operation = await Set(key, new MemoryStream(BitConverter.GetBytes(incrementValue)), duration, null, tags);
            return new GetCacheItemResult(false, operation.Item);
        }

        if (item.Expiration < DateTime.UtcNow)
        {
            await Remove(item.Key);
            item = null;
        }

        if (item == null)
        {
            CacheOperation operation = await Set(key, new MemoryStream(BitConverter.GetBytes(incrementValue)), duration, null, tags);
            return new GetCacheItemResult(false, operation.Item);
        }

        lock (item)
        {
            byte[] valueArray = item.Value.ToArray();
            int value = BitConverter.ToInt32(valueArray);
            byte[] data = BitConverter.GetBytes(value + incrementValue);
            item.Value.Position = 0;
            item.Value.Write(data);
        }

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
    internal Task Remove(MessagingClient client, string key, bool notifyCluster)
    {
        _items.TryRemove(key, out var item);
        Rider.Cache.RemoveEvent.Trigger(client, key);

        if (notifyCluster)
            ClusterNotifier.SendRemove(key);

        if (item != null && item.IsPersistent)
        {
            string directory = $"{Rider.Options.DataPath}/Cache/";
            try
            {
                File.Delete(directory + key + ".hci");
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
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
        List<string> removingKeys = new List<string>();
        List<string> removingFiles = new List<string>();
        foreach (KeyValuePair<string, HorseCacheItem> pair in _items)
        {
            if (pair.Value.Tags.Contains(tagName, StringComparer.CurrentCultureIgnoreCase))
            {
                removingKeys.Add(pair.Key);
                if (pair.Value.IsPersistent)
                    removingFiles.Add(pair.Key);
            }
        }

        foreach (string key in removingKeys)
            _items.TryRemove(key, out _);

        Rider.Cache.PurgeEvent.Trigger(client);

        if (notifyCluster)
            ClusterNotifier.SendPurge(tagName);

        if (removingFiles.Count == 0)
            return;

        string directory = $"{Rider.Options.DataPath}/Cache/";
        if (!Directory.Exists(directory))
            return;

        foreach (string key in removingFiles)
        {
            try
            {
                File.Delete(directory + key + ".hci");
            }
            catch
            {
            }
        }
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
    internal Task Purge(MessagingClient client, bool notifyCluster)
    {
        bool hasPersistentCache = _items.Values.Any(x => x.IsPersistent);

        _items.Clear();
        Rider.Cache.PurgeEvent.Trigger(client);

        if (notifyCluster)
            ClusterNotifier.SendPurge(null);

        if (!hasPersistentCache)
            return Task.CompletedTask;

        string directory = $"{Rider.Options.DataPath}/Cache/";
        if (Directory.Exists(directory))
        {
            string[] filenames = Directory.GetFiles(directory);
            foreach (string filename in filenames)
            {
                try
                {
                    File.Delete(filename);
                }
                catch
                {
                }
            }
        }

        return Task.CompletedTask;
    }

    #endregion

    #region Persistence

    private void LoadPersistentItems()
    {
        try
        {
            string directory = $"{Rider.Options.DataPath}/Cache/";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                return;
            }

            List<string> expiredFiles = new List<string>();

            string[] filenames = Directory.GetFiles(directory);
            foreach (string filename in filenames)
            {
                using FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                using BinaryReader reader = new BinaryReader(fs, System.Text.Encoding.UTF8);
                HorseCacheItem item = new HorseCacheItem();
                item.Key = reader.ReadString();
                item.Expiration = reader.ReadInt64().ToUnixDate();
                item.ExpirationWarnCount = reader.ReadInt32();

                if (item.Expiration < DateTime.UtcNow)
                {
                    expiredFiles.Add(filename);
                    continue;
                }

                bool hasWarningDate = reader.ReadBoolean();
                if (hasWarningDate)
                    item.ExpirationWarning = reader.ReadInt64().ToUnixDate();

                item.IsPersistent = true;
                int tagCount = reader.ReadInt32();
                item.Tags = new string[tagCount];
                for (int i = 0; i < tagCount; i++)
                    item.Tags[i] = reader.ReadString();

                int dataLength = reader.ReadInt32();
                item.Value = new MemoryStream(reader.ReadBytes(dataLength));

                _items.TryAdd(item.Key, item);
            }

            foreach (string expiredFile in expiredFiles)
                File.Delete(expiredFile);
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.LoadPersistentCache, "Load Persistent Cache Items", e);
        }
    }

    private void WritePersistentItem(HorseCacheItem item)
    {
        string directory = $"{Rider.Options.DataPath}/Cache";
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string filename = $"{directory}/{item.Key}.hci";

        using FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write);
        using BinaryWriter writer = new BinaryWriter(fs, System.Text.Encoding.UTF8);

        writer.Write(item.Key);
        writer.Write(item.Expiration.ToUnixMilliseconds());
        writer.Write(item.ExpirationWarnCount);
        writer.Write(item.ExpirationWarning.HasValue);
        if (item.ExpirationWarning.HasValue)
            writer.Write(item.ExpirationWarning.Value.ToUnixMilliseconds());

        writer.Write(item.Tags.Length);
        foreach (string tag in item.Tags)
            writer.Write(tag);

        writer.Write((int)item.Value.Length);
        writer.Write(item.Value.ToArray());

        writer.Flush();
        fs.Close();
    }

    #endregion
}