using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Protocol.Models;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;
using Horse.Server;

namespace Horse.Messaging.Server.Cache;

/// <summary>Cache item returned by the original asynchronous API.</summary>
public record GetCacheItemResult(bool IsFirstWarningReceiver, HorseCacheItem Item);

/// <summary>Allocation-free result for synchronous cache lookups.</summary>
public readonly record struct CacheReadResult(bool IsFirstWarningReceiver, HorseCacheItem Item);

/// <summary>Horse cache manager.</summary>
public class HorseCache : IDisposable, IAsyncDisposable
{
    private sealed class Partition
    {
        internal readonly object Gate = new();
        internal readonly CacheExpirationIndex Expirations = new();
        internal readonly Dictionary<string, HashSet<string>> Tags = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>How a caller's stream becomes an immutable cache payload.</summary>
    private enum PayloadMode : byte
    {
        /// <summary>The caller keeps using its stream, so the bytes are copied.</summary>
        Copy,

        /// <summary>The cache created the stream itself; it is already read-only and unshared.</summary>
        Owned,

        /// <summary>
        /// The caller hands over a buffer it never writes again (a received protocol
        /// message). An exposed, compact buffer is wrapped read-only without copying.
        /// </summary>
        Adopt
    }

    // About 1 ms of gate time per partition (~0.9 µs per removal); with the 1 s
    // timer this drains up to ~65k expirations per second.
    private const int MaxExpirationsPerPartitionPerPass = 1024;

    private readonly Partition[] _partitions = CreatePartitions();
    private readonly object _lifecycleGate = new();
    private readonly Action<HorseCacheItem> _metadataChanged;
    private readonly ConcurrentDictionary<string, HorseCacheItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private Timer _timer;
    private bool _initialized;
    private string _cacheDirectory;
    private CachePersistenceWriter _persistence;
    private HorseServer _attachedServer;
    private int _keyCount;
    private int _cleanupRunning;
    private int _disposed;
    private Task _disposeTask;

    /// <summary>Cache authorizations.</summary>
    public ArrayContainer<ICacheAuthorization> Authorizations { get; } = new();
    /// <summary>Cache options.</summary>
    public HorseCacheOptions Options { get; } = new();
    /// <summary>Root rider.</summary>
    public HorseRider Rider { get; }
    /// <summary>Cache get event manager.</summary>
    public EventManager GetEvent { get; }
    /// <summary>Cache set event manager.</summary>
    public EventManager SetEvent { get; }
    /// <summary>Cache remove event manager.</summary>
    public EventManager RemoveEvent { get; }
    /// <summary>Cache purge event manager.</summary>
    public EventManager PurgeEvent { get; }
    /// <summary>Current number of stored entries, including entries awaiting expiration cleanup.</summary>
    public int Count => Volatile.Read(ref _keyCount);
    internal CacheClusterNotifier ClusterNotifier { get; }

    /// <summary>Creates a cache manager.</summary>
    public HorseCache(HorseRider rider)
    {
        Rider = rider;
        _metadataChanged = UpdateMetadata;
        ClusterNotifier = new CacheClusterNotifier(this, rider.Cluster);
        GetEvent = new EventManager(rider, HorseEventType.CacheGet);
        SetEvent = new EventManager(rider, HorseEventType.CacheSet);
        RemoveEvent = new EventManager(rider, HorseEventType.CacheRemove);
        PurgeEvent = new EventManager(rider, HorseEventType.CachePurge);
    }

    /// <summary>Loads persistent entries and starts indexed expiration cleanup.</summary>
    public void Initialize()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            AttachServer();
            if (_initialized)
                return;
            _cacheDirectory = Path.Combine(Rider.Options.DataPath, "Cache");
            _persistence = new CachePersistenceWriter(_cacheDirectory, Options.AtomicPersistentWrites);
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                LoadPersistentItems();
            }
            catch (Exception e)
            {
                // An unavailable persistence directory must not disable the
                // in-memory cache. Individual persistent writes still fail.
                Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.LoadPersistentCache, "Load persistent cache directory", e);
            }
            _initialized = true;
            _timer = new Timer(_ => RemoveExpiredKeys(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    private void AttachServer()
    {
        HorseServer server = Rider.Server;
        if (ReferenceEquals(_attachedServer, server))
            return;
        if (_attachedServer != null)
        {
            _attachedServer.OnStopped -= OnServerStopped;
            _attachedServer.OnStarted -= OnServerStarted;
        }
        _attachedServer = server;
        if (server != null)
        {
            server.OnStopped += OnServerStopped;
            server.OnStarted += OnServerStarted;
        }
    }

    private void OnServerStopped(HorseServer server)
    {
        Task maintenance;
        Task persistence;
        lock (_lifecycleGate)
        {
            if (_disposed != 0 || !ReferenceEquals(server, _attachedServer))
                return;
            maintenance = _timer?.DisposeAsync().AsTask() ?? Task.CompletedTask;
            _timer = null;
            persistence = _persistence?.FlushAsync() ?? Task.CompletedTask;
        }
        // Stop is reversible: retain entries and the lazy persistence writer.
        // Explicit Dispose remains the permanent end of the cache lifetime.
        Task.WhenAll(maintenance, persistence).GetAwaiter().GetResult();
    }

    private void OnServerStarted(HorseServer server)
    {
        lock (_lifecycleGate)
        {
            if (_disposed != 0 || !ReferenceEquals(server, _attachedServer))
                return;
            _timer ??= new Timer(_ => RemoveExpiredKeys(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    private static Partition[] CreatePartitions()
    {
        Partition[] partitions = new Partition[64];
        for (int i = 0; i < partitions.Length; i++)
            partitions[i] = new Partition();
        return partitions;
    }

    private Partition GetPartition(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _partitions[(uint)StringComparer.OrdinalIgnoreCase.GetHashCode(key) % (uint)_partitions.Length];
    }

    private bool ReserveKey()
    {
        while (true)
        {
            int count = Volatile.Read(ref _keyCount);
            int maximum = Options.MaximumKeys;
            if (maximum > 0 && count >= maximum)
                return false;
            if (Interlocked.CompareExchange(ref _keyCount, count + 1, count) == count)
                return true;
        }
    }

    private static void IndexTags(Partition partition, HorseCacheItem item)
    {
        item.IndexedTags = item.TagValues;
        foreach (string tag in item.IndexedTags)
        {
            if (tag == null)
                continue;
            if (!partition.Tags.TryGetValue(tag, out HashSet<string> keys))
                partition.Tags.Add(tag, keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            keys.Add(item.Key);
        }
    }

    private static void UnindexTags(Partition partition, HorseCacheItem item)
    {
        foreach (string tag in item.IndexedTags)
        {
            if (tag == null || !partition.Tags.TryGetValue(tag, out HashSet<string> keys))
                continue;
            keys.Remove(item.Key);
            if (keys.Count == 0)
                partition.Tags.Remove(tag);
        }
    }

    private void UpdateMetadata(HorseCacheItem item)
    {
        Partition partition = GetPartition(item.CacheKey);
        lock (partition.Gate)
        {
            if (_disposed != 0 || !_items.TryGetValue(item.CacheKey, out HorseCacheItem current) || !ReferenceEquals(item, current))
                return;
            UnindexTags(partition, item);
            IndexTags(partition, item);
            partition.Expirations.Set(item);
        }
    }

    // Under a key's partition gate: dictionary and index mutations, persistence
    // admission and cluster notification. Admitting persistence and replication
    // there keeps both in commit order for the key. Payload preparation happens
    // before the gate; events and persistence waits happen after it is released.
    // Readers remain lock-free.
    private bool RemoveCurrent(Partition partition, HorseCacheItem expected, out Task persistence)
    {
        persistence = Task.CompletedTask;
        if (!_items.TryGetValue(expected.Key, out HorseCacheItem current) || !ReferenceEquals(current, expected))
            return false;
        _items.TryRemove(expected.Key, out _);
        Interlocked.Decrement(ref _keyCount);
        partition.Expirations.Remove(expected.Key);
        UnindexTags(partition, expected);
        expected.MetadataChanged = null;
        if (expected.IsPersistent)
            persistence = _persistence.DeleteAsync(expected.CacheKey ?? expected.Key);
        return true;
    }

    /// <summary>Timer pass: removes at most <see cref="MaxExpirationsPerPartitionPerPass"/> entries per partition.</summary>
    private void RemoveExpiredKeys() => RemoveExpiredKeysCore(MaxExpirationsPerPartitionPerPass);

    /// <summary>
    /// The per-partition limit bounds how long one pass holds a gate when many
    /// entries expire together. Readers already treat expired entries as missing,
    /// so a remainder simply waits for the next pass.
    /// </summary>
    private void RemoveExpiredKeysCore(int maxPerPartition)
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _cleanupRunning, 1) != 0)
            return;
        try
        {
            DateTime now = DateTime.UtcNow;
            foreach (Partition partition in _partitions)
            {
                lock (partition.Gate)
                {
                    if (_disposed != 0)
                        return;
                    for (int removed = 0; removed < maxPerPartition && partition.Expirations.TryPeek(out HorseCacheItem item) && item.Expiration < now; removed++)
                    {
                        if (RemoveCurrent(partition, item, out Task persistence))
                            ObservePersistence(persistence);
                        else
                            RepairExpiration(partition, item.Key);
                    }
                }
            }
        }
        finally
        {
            Volatile.Write(ref _cleanupRunning, 0);
        }
    }

    // Heap entries are always the key's current item. Should that ever not hold,
    // re-point the entry so an unbounded pass cannot spin on it under the gate.
    private void RepairExpiration(Partition partition, string key)
    {
        if (_items.TryGetValue(key, out HorseCacheItem current))
            partition.Expirations.Set(current);
        else
            partition.Expirations.Remove(key);
    }

    private void ObservePersistence(Task task)
    {
        if (!task.IsCompletedSuccessfully)
            _ = ReportPersistenceFailure(task);
    }

    private async Task ReportPersistenceFailure(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.LoadPersistentCache, "Expire persistent cache item", e);
        }
    }

    /// <summary>Gets all live cache keys.</summary>
    public List<CacheInformation> GetCacheKeys() => GetCacheKeys(null, 0, 0, null);

    /// <summary>
    /// Filters before allocating result metadata. A positive limit or offset
    /// selects a key-ordered page; limit zero returns all remaining matches.
    /// Pages are weakly consistent when concurrent writes change the cache.
    /// </summary>
    public List<CacheInformation> GetCacheKeys(string filter, int offset = 0, int limit = 0)
        => GetCacheKeys(filter, offset, limit, null);

    internal List<CacheInformation> GetCacheKeys(string filter, int offset, int limit, Func<string, bool> canGet)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        RemoveExpiredKeys();
        DateTime now = DateTime.UtcNow;
        bool filtered = !string.IsNullOrEmpty(filter) && filter != "*";
        int capacity = limit > 0 ? Math.Min(limit, 4096) : filtered || canGet != null ? 0 : Count;
        List<CacheInformation> result = new(capacity);
        List<HorseCacheItem> page = offset > 0 || limit > 0 ? new List<HorseCacheItem>() : null;
        int take = (int)Math.Min(int.MaxValue, (long)offset + limit);
        // Keep only the smallest offset+limit keys instead of allocating and
        // sorting a snapshot of every matching entry for a small page.
        PriorityQueue<HorseCacheItem, string> selected = limit > 0
            ? new PriorityQueue<HorseCacheItem, string>(Comparer<string>.Create((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b, a)))
            : null;
        foreach (KeyValuePair<string, HorseCacheItem> pair in _items)
        {
            HorseCacheItem item = pair.Value;
            if (item.Expiration < now || (filtered && !Filter.CheckMatch(item.Key, filter)) || (canGet != null && !canGet(item.Key)))
                continue;
            if (page == null)
                result.Add(CreateInformation(item));
            else if (selected != null)
            {
                if (selected.Count < take)
                    selected.Enqueue(item, item.Key);
                else if (StringComparer.OrdinalIgnoreCase.Compare(item.Key, selected.Peek().Key) < 0)
                    selected.DequeueEnqueue(item, item.Key);
            }
            else
                page.Add(item);
        }
        if (page != null)
        {
            if (selected != null)
                foreach (var entry in selected.UnorderedItems)
                    page.Add(entry.Element);
            page.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));
            int end = limit == 0 ? page.Count : (int)Math.Min(page.Count, (long)offset + limit);
            for (int i = offset; i < end; i++)
                result.Add(CreateInformation(page[i]));
        }
        return result;
    }

    private static CacheInformation CreateInformation(HorseCacheItem item) => new()
    {
        Key = item.Key,
        Expiration = item.Expiration.ToUnixSeconds(),
        WarningDate = item.ExpirationWarning?.ToUnixSeconds() ?? 0,
        WarnCount = item.ExpirationWarnCount,
        Tags = item.TagValues.Length == 0 ? Array.Empty<string>() : (string[])item.TagValues.Clone()
    };

    /// <summary>Adds or replaces a string entry, waiting for its persistent write if requested.</summary>
    public CacheOperation Set(string key, string value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
        => Wait(SetAsync(key, value, duration, expirationWarning, tags, persistent));

    /// <summary>Adds or replaces an entry, waiting for its persistent write if requested.</summary>
    public CacheOperation Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
        => Wait(SetAsyncCore(null, true, key, value, duration, expirationWarning, tags, persistent, PayloadMode.Copy));

    /// <summary>Adds or replaces a string entry without blocking on persistent IO.</summary>
    public ValueTask<CacheOperation> SetAsync(string key, string value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return SetAsyncCore(null, true, key, new MemoryStream(bytes, 0, bytes.Length, false, true), duration, expirationWarning, tags, persistent, PayloadMode.Owned);
    }

    /// <summary>Copies the supplied value once and asynchronously waits for persistent IO.</summary>
    public ValueTask<CacheOperation> SetAsync(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
        => SetAsyncCore(null, true, key, value, duration, expirationWarning, tags, persistent, PayloadMode.Copy);

    /// <summary>
    /// Stores the content of a received protocol message. The cache takes over the
    /// message's buffer, so the caller must not write to <paramref name="value"/> afterwards.
    /// The entry is committed before this returns; only persistent IO completes later.
    /// </summary>
    internal ValueTask<CacheOperation> SetAsync(MessagingClient client, bool notifyCluster, string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
        => SetAsyncCore(client, notifyCluster, key, value, duration, expirationWarning, tags, persistent, PayloadMode.Adopt);

    private ValueTask<CacheOperation> SetAsyncCore(MessagingClient client, bool notifyCluster, string key, MemoryStream value, TimeSpan duration, TimeSpan? warning, string[] tags, bool persistent, PayloadMode payloadMode)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Partition partition = GetPartition(key);
        if (persistent)
            CachePersistenceWriter.ValidateKey(key);
        if (Options.ValueMaxSize > 0 && (value?.Length ?? 0) > Options.ValueMaxSize)
            return new ValueTask<CacheOperation>(new CacheOperation(CacheResult.ItemSizeLimit, null));

        // Copying the payload and formatting metadata happen before the gate.
        HorseCacheItem item = CreateItem(key, value, duration, warning, tags, persistent, payloadMode);
        CacheResult result = CommitSet(partition, key, item, notifyCluster, out Task persistence);
        if (result == CacheResult.KeyLimit)
        {
            // Expired entries still count towards the limit until removed; free them all.
            RemoveExpiredKeysCore(int.MaxValue);
            result = CommitSet(partition, key, item, notifyCluster, out persistence);
        }

        if (result != CacheResult.Ok)
            return new ValueTask<CacheOperation>(new CacheOperation(result, null));

        SetEvent.Trigger(client, key);
        CacheOperation operation = new CacheOperation(CacheResult.Ok, item);
        return persistence.IsCompletedSuccessfully ? new ValueTask<CacheOperation>(operation) : AwaitPersistence(operation, persistence);
    }

    private HorseCacheItem CreateItem(string key, MemoryStream value, TimeSpan duration, TimeSpan? warning, string[] tags, bool persistent, PayloadMode payloadMode)
    {
        TimeSpan d = duration == TimeSpan.Zero ? Options.DefaultDuration : duration;
        if (Options.MinimumDuration > TimeSpan.Zero && d < Options.MinimumDuration)
            d = Options.MinimumDuration;
        else if (Options.MaximumDuration > TimeSpan.Zero && d > Options.MaximumDuration)
            d = Options.MaximumDuration;
        DateTime now = DateTime.UtcNow;
        return new HorseCacheItem
        {
            Key = key,
            Tags = tags,
            Expiration = now + d,
            ExpirationWarning = now + (warning ?? (Options.ExpirationWarningIsEnabled ? Options.DefaultExpirationWarning : d)),
            Value = CreatePayload(value, payloadMode),
            IsPersistent = persistent
        };
    }

    private static MemoryStream CreatePayload(MemoryStream value, PayloadMode mode)
    {
        if (mode == PayloadMode.Owned)
            return value;

        // Received messages own an exactly sized buffer (HorseProtocolReader allocates
        // the content length). Wrapping it avoids a second payload-sized allocation,
        // which is a large-object allocation for values over 85 KB. A buffer with
        // notable spare capacity is copied so the cache does not retain the slack.
        if (mode == PayloadMode.Adopt && value != null && value.TryGetBuffer(out ArraySegment<byte> buffer) &&
            buffer.Array.Length - buffer.Count <= buffer.Count / 8)
            return new MemoryStream(buffer.Array, buffer.Offset, buffer.Count, false, true);

        byte[] bytes = value?.ToArray() ?? Array.Empty<byte>();
        return new MemoryStream(bytes, 0, bytes.Length, false, true);
    }

    /// <summary>
    /// Publishes a prepared, not yet stored item under the key's gate. The caller
    /// triggers the event and waits for persistence after the gate is released.
    /// </summary>
    private CacheResult CommitSet(Partition partition, string key, HorseCacheItem item, bool notifyCluster, out Task persistence)
    {
        persistence = Task.CompletedTask;
        lock (partition.Gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _items.TryGetValue(key, out HorseCacheItem previous);
            if (previous == null && !ReserveKey())
                return CacheResult.KeyLimit;
            if (previous != null)
            {
                // Keep the stored casing; a persistent entry's file is named after it.
                item.Key = previous.CacheKey ?? key;
                UnindexTags(partition, previous);
                previous.MetadataChanged = null;
            }
            item.CacheKey = item.Key;
            item.MetadataGate = partition.Gate;
            item.MetadataChanged = _metadataChanged;
            _items[key] = item;
            partition.Expirations.Set(item);
            IndexTags(partition, item);
            if (item.IsPersistent)
                persistence = _persistence.WriteAsync(item);
            else if (previous?.IsPersistent == true)
                persistence = _persistence.DeleteAsync(previous.CacheKey ?? previous.Key);
            if (notifyCluster)
                ClusterNotifier.SendSet(item);
            return CacheResult.Ok;
        }
    }

    private static async ValueTask<T> AwaitPersistence<T>(T result, Task persistence)
    {
        await persistence.ConfigureAwait(false);
        return result;
    }

    private static T Wait<T>(ValueTask<T> task) => task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();

    private static void Wait(Task task)
    {
        if (!task.IsCompletedSuccessfully)
            task.GetAwaiter().GetResult();
    }

    /// <summary>Looks up a live entry without allocating a Task or a result object.</summary>
    public bool TryGet(string key, out CacheReadResult result)
    {
        if (Volatile.Read(ref _disposed) != 0 || !_items.TryGetValue(key, out HorseCacheItem item))
        {
            result = default;
            return false;
        }
        DateTime now = DateTime.UtcNow;
        if (item.Expiration < now)
        {
            Partition partition = GetPartition(key);
            bool removed;
            Task persistence = Task.CompletedTask;
            lock (partition.Gate)
            {
                removed = _disposed == 0 && RemoveCurrent(partition, item, out persistence);
                if (removed)
                    ClusterNotifier.SendRemove(item.Key);
            }
            if (removed)
            {
                RemoveEvent.Trigger((MessagingClient)null, item.Key);
                ObservePersistence(persistence);
            }
            result = default;
            return false;
        }
        bool first = item.ExpirationWarning.HasValue && item.ExpirationWarning.Value < now && Interlocked.Increment(ref item.WarningState.Count) == 1;
        result = new CacheReadResult(first, item);
        return true;
    }

    /// <summary>Compatibility asynchronous lookup. Internal readers can use TryGet to avoid allocation.</summary>
    public Task<GetCacheItemResult> Get(string key)
        => Task.FromResult(TryGet(key, out CacheReadResult result) ? new GetCacheItemResult(result.IsFirstWarningReceiver, result.Item) : null);

    /// <summary>
    /// Increments a four-byte integer value, waiting for its persistent write.
    /// New and expired entries start at incrementValue.
    /// </summary>
    public GetCacheItemResult GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null)
        => Wait(GetIncrementalAsync(key, duration, incrementValue, tags));

    /// <summary>
    /// Increments a four-byte integer value without blocking on persistent IO.
    /// New and expired entries start at incrementValue. The new value is visible
    /// before this returns; only persistent IO completes later.
    /// </summary>
    public ValueTask<GetCacheItemResult> GetIncrementalAsync(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Partition partition = GetPartition(key);
        Task expiredDeletion = Task.CompletedTask;
        Task written = Task.CompletedTask;
        string expiredKey = null;
        bool created = false;
        HorseCacheItem next;
        lock (partition.Gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _items.TryGetValue(key, out HorseCacheItem item);
            if (item == null || item.Expiration < DateTime.UtcNow)
            {
                if (item != null && RemoveCurrent(partition, item, out expiredDeletion))
                {
                    expiredKey = item.Key;
                    ClusterNotifier.SendRemove(expiredKey);
                }
                byte[] bytes = BitConverter.GetBytes(incrementValue);
                next = CreateItem(key, new MemoryStream(bytes, 0, bytes.Length, false, true), duration, null, tags, false, PayloadMode.Owned);
                created = CommitSet(partition, key, next, true, out written) == CacheResult.Ok;
                if (!created)
                    next = null;
            }
            else
            {
                if (item.Value.Length != sizeof(int))
                    throw new InvalidOperationException("Incremental cache values must contain exactly four bytes.");
                int current;
                if (item.Value.TryGetBuffer(out ArraySegment<byte> segment))
                    current = BitConverter.ToInt32(segment.AsSpan());
                else
                    current = ReadInteger(item.Value);
                byte[] bytes = BitConverter.GetBytes(unchecked(current + incrementValue));
                next = item.WithValue(new MemoryStream(bytes, 0, bytes.Length, false, true));
                item.MetadataChanged = null;
                _items[key] = next;
                partition.Expirations.Set(next);
                if (next.IsPersistent)
                    written = _persistence.WriteAsync(next);
                ClusterNotifier.SendSet(next);
            }
        }

        if (expiredKey != null)
            RemoveEvent.Trigger((MessagingClient)null, expiredKey);
        if (created)
            SetEvent.Trigger((MessagingClient)null, key);

        GetCacheItemResult result = new GetCacheItemResult(false, next);
        Task persistence = written.IsCompletedSuccessfully ? expiredDeletion
            : expiredDeletion.IsCompletedSuccessfully ? written
            : Task.WhenAll(expiredDeletion, written);
        return persistence.IsCompletedSuccessfully ? new ValueTask<GetCacheItemResult>(result) : AwaitPersistence(result, persistence);
    }

    private static int ReadInteger(MemoryStream value)
    {
        value.Position = 0;
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        value.ReadExactly(bytes);
        return BitConverter.ToInt32(bytes);
    }

    /// <summary>Removes an entry and waits for its persistent deletion.</summary>
    public void Remove(string key) => Wait(RemoveCore(null, key, true));

    /// <summary>
    /// Removes an entry without blocking on persistent IO. The entry is gone
    /// before this returns; the task completes when its file is deleted.
    /// </summary>
    public Task RemoveAsync(string key) => RemoveCore(null, key, true);

    internal Task RemoveAsync(MessagingClient client, string key, bool notifyCluster) => RemoveCore(client, key, notifyCluster);

    private Task RemoveCore(MessagingClient client, string key, bool notifyCluster)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Partition partition = GetPartition(key);
        Task persistence = Task.CompletedTask;
        lock (partition.Gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (_items.TryGetValue(key, out HorseCacheItem item))
                RemoveCurrent(partition, item, out persistence);
            if (notifyCluster)
                ClusterNotifier.SendRemove(key);
        }
        RemoveEvent.Trigger(client, key);
        return persistence;
    }

    /// <summary>Purges entries bearing a tag using the tag index, waiting for persistent deletions.</summary>
    public void PurgeByTag(string tagName) => Wait(PurgeByTagCore(tagName, null, true));

    /// <summary>
    /// Purges entries bearing a tag without blocking on persistent IO. The entries
    /// are gone before this returns; the task completes when their files are deleted.
    /// </summary>
    public Task PurgeByTagAsync(string tagName) => PurgeByTagCore(tagName, null, true);

    internal Task PurgeByTagAsync(string tagName, MessagingClient client, bool notifyCluster) => PurgeByTagCore(tagName, client, notifyCluster);

    private Task PurgeByTagCore(string tagName, MessagingClient client, bool notifyCluster)
    {
        ArgumentNullException.ThrowIfNull(tagName);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        List<Task> pending = null;
        foreach (Partition partition in _partitions)
        {
            lock (partition.Gate)
            {
                ObjectDisposedException.ThrowIf(_disposed != 0, this);
                if (!partition.Tags.TryGetValue(tagName, out HashSet<string> indexed))
                    continue;
                string[] keys = new string[indexed.Count];
                indexed.CopyTo(keys);
                foreach (string key in keys)
                    if (_items.TryGetValue(key, out HorseCacheItem item) &&
                        RemoveCurrent(partition, item, out Task persistence) && !persistence.IsCompletedSuccessfully)
                        (pending ??= new List<Task>()).Add(persistence);
            }
        }
        PurgeEvent.Trigger(client);
        if (notifyCluster)
            ClusterNotifier.SendPurge(tagName);
        return pending == null ? Task.CompletedTask : Task.WhenAll(pending);
    }

    /// <summary>Purges all entries and waits for persistent files to be deleted.</summary>
    public void Purge() => Wait(PurgeCore(null, true));

    /// <summary>
    /// Purges all entries without blocking on persistent IO. The entries are gone
    /// before this returns; the task completes when the files are deleted.
    /// </summary>
    public Task PurgeAsync() => PurgeCore(null, true);

    internal Task PurgeAsync(MessagingClient client, bool notifyCluster) => PurgeCore(client, notifyCluster);

    private Task PurgeCore(MessagingClient client, bool notifyCluster)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Task persistence;
        foreach (Partition partition in _partitions)
            Monitor.Enter(partition.Gate);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            foreach (var pair in _items)
                pair.Value.MetadataChanged = null;
            _items.Clear();
            Volatile.Write(ref _keyCount, 0);
            foreach (Partition partition in _partitions)
            {
                partition.Expirations.Clear();
                partition.Tags.Clear();
            }
            persistence = _persistence?.PurgeAsync() ?? Task.CompletedTask;
            // Sent under the gates so replicas apply the purge before any later write.
            if (notifyCluster)
                ClusterNotifier.SendPurge(null);
        }
        finally
        {
            for (int i = _partitions.Length - 1; i >= 0; i--)
                Monitor.Exit(_partitions[i].Gate);
        }
        PurgeEvent.Trigger(client);
        return persistence;
    }

    private void LoadPersistentItems()
    {
        foreach (string filename in Directory.EnumerateFiles(_cacheDirectory, "*.hci"))
        {
            try
            {
                HorseCacheItem item;
                using (FileStream fs = new(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new(fs, Encoding.UTF8))
                {
                    item = new HorseCacheItem { Key = reader.ReadString(), Expiration = reader.ReadInt64().ToUnixDate(), ExpirationWarnCount = reader.ReadInt32() };
                    CachePersistenceWriter.ValidateKey(item.Key);
                    if (item.Expiration < DateTime.UtcNow)
                    {
                        fs.Dispose();
                        File.Delete(filename);
                        continue;
                    }
                    if (reader.ReadBoolean())
                        item.ExpirationWarning = reader.ReadInt64().ToUnixDate();
                    item.IsPersistent = true;
                    int tagCount = reader.ReadInt32();
                    if (tagCount < 0 || tagCount > fs.Length - fs.Position)
                        throw new InvalidDataException("Invalid cache tag count.");
                    string[] tags = new string[tagCount];
                    for (int i = 0; i < tags.Length; i++)
                        tags[i] = reader.ReadString();
                    item.Tags = tags;
                    int length = reader.ReadInt32();
                    if (length < 0 || length > fs.Length - fs.Position)
                        throw new InvalidDataException("Invalid cache payload length.");
                    if (Options.ValueMaxSize > 0 && length > Options.ValueMaxSize)
                        continue;
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length)
                        throw new EndOfStreamException();
                    item.Value = new MemoryStream(bytes, 0, bytes.Length, false, true);
                    item.CacheKey = item.Key;
                    item.MetadataGate = GetPartition(item.Key).Gate;
                    item.MetadataChanged = _metadataChanged;
                }
                Partition partition = GetPartition(item.Key);
                lock (partition.Gate)
                {
                    if (!_items.ContainsKey(item.Key) && ReserveKey())
                    {
                        _items[item.Key] = item;
                        partition.Expirations.Set(item);
                        IndexTags(partition, item);
                    }
                }
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Critical, HorseLogEvents.LoadPersistentCache, "Load persistent cache item: " + filename, e);
            }
        }
    }

    /// <summary>Drains queued persistence operations without stopping the cache.</summary>
    public Task FlushAsync() => _persistence?.FlushAsync() ?? Task.CompletedTask;

    /// <summary>Stops maintenance and drains accepted persistent writes.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>Stops maintenance and drains accepted persistent writes.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_lifecycleGate)
        {
            if (_disposeTask == null)
            {
                Volatile.Write(ref _disposed, 1);
                if (_attachedServer != null)
                {
                    _attachedServer.OnStopped -= OnServerStopped;
                    _attachedServer.OnStarted -= OnServerStarted;
                    _attachedServer = null;
                }
                _disposeTask = DisposeCoreAsync();
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        if (_timer != null)
            await _timer.DisposeAsync().ConfigureAwait(false);
        // Wait for any mutator that passed the disposed check to submit its IO.
        foreach (Partition partition in _partitions)
            Monitor.Enter(partition.Gate);
        try
        {
            foreach (var pair in _items)
                pair.Value.MetadataChanged = null;
        }
        finally
        {
            for (int i = _partitions.Length - 1; i >= 0; i--)
                Monitor.Exit(_partitions[i].Gate);
        }
        if (_persistence != null)
            await _persistence.DisposeAsync().ConfigureAwait(false);
        GetEvent.Dispose();
        SetEvent.Dispose();
        RemoveEvent.Dispose();
        PurgeEvent.Dispose();
    }
}
