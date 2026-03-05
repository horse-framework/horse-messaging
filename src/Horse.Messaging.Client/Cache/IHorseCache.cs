using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Cache;

/// <inheritdoc />
public interface IHorseCache<TIdentifier> : IHorseCache
{
}

/// <summary>
/// Client-side cache management interface for Horse server cache operations.
/// </summary>
public interface IHorseCache
{
    #region Get

    /// <summary>
    /// Gets a cached value deserialized to the specified type.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a cached value as a string.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a cached value as a byte array.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken);

    #endregion

    #region GetIncrementalValue

    /// <summary>
    /// Gets or creates an incremental counter value. Increments by 1 with no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Gets or creates an incremental counter value with a custom increment step.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="increment">Increment step value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken);

    /// <summary>
    /// Gets or creates an incremental counter value with an expiration duration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="duration">Time-to-live for the counter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>
    /// Gets or creates an incremental counter value with a custom increment step and expiration duration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="duration">Time-to-live for the counter.</param>
    /// <param name="increment">Increment step value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment, CancellationToken cancellationToken);

    #endregion

    #region List

    /// <summary>
    /// Lists all cache entries on the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken);

    /// <summary>
    /// Lists cache entries matching a filter pattern.
    /// </summary>
    /// <param name="filter">Filter pattern (supports wildcards).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken);

    #endregion

    #region Set<TData>

    /// <summary>
    /// Sets a cache value with no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Value to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a cache value with tags and persistence option, no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Value to cache.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a cache value with an expiration duration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Value to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a cache value with expiration, tags, and persistence option.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Value to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a cache value with full options including expiration warning.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Value to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="expirationWarningDuration">Duration before expiration to trigger a warning event.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    #endregion

    #region SetString

    /// <summary>
    /// Sets a string cache value with no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">String value to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a string cache value with tags and persistence option, no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">String value to cache.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a string cache value with an expiration duration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">String value to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a string cache value with expiration, tags, and persistence option.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">String value to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a string cache value with full options including expiration warning.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">String value to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="expirationWarningDuration">Duration before expiration to trigger a warning event.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    #endregion

    #region SetData

    /// <summary>
    /// Sets a binary cache value with no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Binary data to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a binary cache value with tags and persistence option, no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Binary data to cache.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a binary cache value with an expiration duration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Binary data to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a binary cache value with expiration, tags, and persistence option.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Binary data to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a binary cache value with full options including expiration warning.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="data">Binary data to cache.</param>
    /// <param name="duration">Time-to-live for the cache entry.</param>
    /// <param name="expirationWarningDuration">Duration before expiration to trigger a warning event.</param>
    /// <param name="tags">Optional tags for grouping cache entries.</param>
    /// <param name="persistent">If true, persists across server restarts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    #endregion

    #region Remove / Purge

    /// <summary>
    /// Removes a single cache entry by key.
    /// </summary>
    /// <param name="key">Cache key to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Remove(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Removes all cache entries on the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> Purge(CancellationToken cancellationToken);

    /// <summary>
    /// Removes all cache entries matching a specific tag.
    /// </summary>
    /// <param name="tag">Tag to match for removal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken);

    #endregion
}