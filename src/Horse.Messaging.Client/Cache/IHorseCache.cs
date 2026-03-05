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

    /// <inheritdoc cref="Get{TData}(string, CancellationToken)"/>
    Task<HorseCacheData<TData>> Get<TData>(string key)
        => Get<TData>(key, CancellationToken.None);

    /// <summary>
    /// Gets a cached value deserialized to the specified type.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<TData>> Get<TData>(string key, CancellationToken cancellationToken);

    /// <inheritdoc cref="GetString(string, CancellationToken)"/>
    Task<HorseCacheData<string>> GetString(string key)
        => GetString(key, CancellationToken.None);

    /// <summary>
    /// Gets a cached value as a string.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<string>> GetString(string key, CancellationToken cancellationToken);

    /// <inheritdoc cref="GetData(string, CancellationToken)"/>
    Task<HorseCacheData<byte[]>> GetData(string key)
        => GetData(key, CancellationToken.None);

    /// <summary>
    /// Gets a cached value as a byte array.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<byte[]>> GetData(string key, CancellationToken cancellationToken);

    #endregion

    #region GetIncrementalValue

    /// <inheritdoc cref="GetIncrementalValue(string, CancellationToken)"/>
    Task<HorseCacheData<int>> GetIncrementalValue(string key)
        => GetIncrementalValue(key, CancellationToken.None);

    /// <summary>
    /// Gets or creates an incremental counter value. Increments by 1 with no expiration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, CancellationToken cancellationToken);

    /// <inheritdoc cref="GetIncrementalValue(string, int, CancellationToken)"/>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment)
        => GetIncrementalValue(key, increment, CancellationToken.None);

    /// <summary>
    /// Gets or creates an incremental counter value with a custom increment step.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="increment">Increment step value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment, CancellationToken cancellationToken);

    /// <inheritdoc cref="GetIncrementalValue(string, TimeSpan, CancellationToken)"/>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration)
        => GetIncrementalValue(key, duration, CancellationToken.None);

    /// <summary>
    /// Gets or creates an incremental counter value with an expiration duration.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="duration">Time-to-live for the counter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, CancellationToken cancellationToken);

    /// <inheritdoc cref="GetIncrementalValue(string, TimeSpan, int, CancellationToken)"/>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment)
        => GetIncrementalValue(key, duration, increment, CancellationToken.None);

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

    /// <inheritdoc cref="List(CancellationToken)"/>
    Task<HorseModelResult<List<CacheInformation>>> List()
        => List(CancellationToken.None);

    /// <summary>
    /// Lists all cache entries on the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseModelResult<List<CacheInformation>>> List(CancellationToken cancellationToken);

    /// <inheritdoc cref="List(string, CancellationToken)"/>
    Task<HorseModelResult<List<CacheInformation>>> List(string filter)
        => List(filter, CancellationToken.None);

    /// <summary>
    /// Lists cache entries matching a filter pattern.
    /// </summary>
    /// <param name="filter">Filter pattern (supports wildcards).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HorseModelResult<List<CacheInformation>>> List(string filter, CancellationToken cancellationToken);

    #endregion

    #region Set<TData>

    /// <inheritdoc cref="Set{TData}(string, TData, CancellationToken)"/>
    Task<HorseResult> Set<TData>(string key, TData data)
        => Set(key, data, CancellationToken.None);

    /// <summary>
    /// Sets a cache value with no expiration.
    /// </summary>
    Task<HorseResult> Set<TData>(string key, TData data, CancellationToken cancellationToken);

    /// <inheritdoc cref="Set{TData}(string, TData, string[], bool, CancellationToken)"/>
    Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent)
        => Set(key, data, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a cache value with tags and persistence option, no expiration.
    /// </summary>
    Task<HorseResult> Set<TData>(string key, TData data, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <inheritdoc cref="Set{TData}(string, TData, TimeSpan, CancellationToken)"/>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration)
        => Set(key, data, duration, CancellationToken.None);

    /// <summary>
    /// Sets a cache value with an expiration duration.
    /// </summary>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, CancellationToken cancellationToken);

    /// <inheritdoc cref="Set{TData}(string, TData, TimeSpan, string[], bool, CancellationToken)"/>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent)
        => Set(key, data, duration, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a cache value with expiration, tags, and persistence option.
    /// </summary>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <inheritdoc cref="Set{TData}(string, TData, TimeSpan, TimeSpan, string[], bool, CancellationToken)"/>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent)
        => Set(key, data, duration, expirationWarningDuration, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a cache value with full options including expiration warning.
    /// </summary>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    #endregion

    #region SetString

    /// <inheritdoc cref="SetString(string, string, CancellationToken)"/>
    Task<HorseResult> SetString(string key, string data)
        => SetString(key, data, CancellationToken.None);

    /// <summary>
    /// Sets a string cache value with no expiration.
    /// </summary>
    Task<HorseResult> SetString(string key, string data, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetString(string, string, string[], bool, CancellationToken)"/>
    Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent)
        => SetString(key, data, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a string cache value with tags and persistence option, no expiration.
    /// </summary>
    Task<HorseResult> SetString(string key, string data, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetString(string, string, TimeSpan, CancellationToken)"/>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration)
        => SetString(key, data, duration, CancellationToken.None);

    /// <summary>
    /// Sets a string cache value with an expiration duration.
    /// </summary>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetString(string, string, TimeSpan, string[], bool, CancellationToken)"/>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent)
        => SetString(key, data, duration, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a string cache value with expiration, tags, and persistence option.
    /// </summary>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetString(string, string, TimeSpan, TimeSpan, string[], bool, CancellationToken)"/>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent)
        => SetString(key, data, duration, expirationWarningDuration, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a string cache value with full options including expiration warning.
    /// </summary>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    #endregion

    #region SetData

    /// <inheritdoc cref="SetData(string, byte[], CancellationToken)"/>
    Task<HorseResult> SetData(string key, byte[] data)
        => SetData(key, data, CancellationToken.None);

    /// <summary>
    /// Sets a binary cache value with no expiration.
    /// </summary>
    Task<HorseResult> SetData(string key, byte[] data, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetData(string, byte[], string[], bool, CancellationToken)"/>
    Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent)
        => SetData(key, data, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a binary cache value with tags and persistence option, no expiration.
    /// </summary>
    Task<HorseResult> SetData(string key, byte[] data, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetData(string, byte[], TimeSpan, CancellationToken)"/>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration)
        => SetData(key, data, duration, CancellationToken.None);

    /// <summary>
    /// Sets a binary cache value with an expiration duration.
    /// </summary>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetData(string, byte[], TimeSpan, string[], bool, CancellationToken)"/>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent)
        => SetData(key, data, duration, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a binary cache value with expiration, tags, and persistence option.
    /// </summary>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags, bool persistent, CancellationToken cancellationToken);

    /// <inheritdoc cref="SetData(string, byte[], TimeSpan, TimeSpan, string[], bool, CancellationToken)"/>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent)
        => SetData(key, data, duration, expirationWarningDuration, tags, persistent, CancellationToken.None);

    /// <summary>
    /// Sets a binary cache value with full options including expiration warning.
    /// </summary>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags, bool persistent, CancellationToken cancellationToken);

    #endregion

    #region Remove / Purge

    /// <inheritdoc cref="Remove(string, CancellationToken)"/>
    Task<HorseResult> Remove(string key)
        => Remove(key, CancellationToken.None);

    /// <summary>
    /// Removes a single cache entry by key.
    /// </summary>
    Task<HorseResult> Remove(string key, CancellationToken cancellationToken);

    /// <inheritdoc cref="Purge(CancellationToken)"/>
    Task<HorseResult> Purge()
        => Purge(CancellationToken.None);

    /// <summary>
    /// Removes all cache entries on the server.
    /// </summary>
    Task<HorseResult> Purge(CancellationToken cancellationToken);

    /// <inheritdoc cref="PurgeByTag(string, CancellationToken)"/>
    Task<HorseResult> PurgeByTag(string tag)
        => PurgeByTag(tag, CancellationToken.None);

    /// <summary>
    /// Removes all cache entries matching a specific tag.
    /// </summary>
    Task<HorseResult> PurgeByTag(string tag, CancellationToken cancellationToken);

    #endregion
}