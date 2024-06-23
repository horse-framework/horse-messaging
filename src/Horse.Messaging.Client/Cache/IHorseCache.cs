using System;
using System.Collections.Generic;
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
/// Cache management implementation for client
/// </summary>
public interface IHorseCache
{
    /// <summary>
    /// Gets an item from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    Task<HorseCacheData<TData>> Get<TData>(string key);

    /// <summary>
    /// Gets a string from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    Task<HorseCacheData<string>> GetString(string key);

    /// <summary>
    /// Gets a incremental integer value from cache.
    /// Each get request increases value by 1.
    /// </summary>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, int increment = 1);

    /// <summary>
    /// Gets a incremental integer value from cache.
    /// Each get request increases value by 1.
    /// </summary>
    Task<HorseCacheData<int>> GetIncrementalValue(string key, TimeSpan duration, int increment = 1);

    /// <summary>
    /// Gets the binary data from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    Task<HorseCacheData<byte[]>> GetData(string key);

    /// <summary>
    /// Lists all cache keys
    /// </summary>
    /// <param name="filter">Cache key filter. Supports * character for filtering.</param>
    /// <returns></returns>
    Task<HorseModelResult<List<CacheInformation>>> List(string filter = null);

    /// <summary>
    /// Sets an item to cache store with specified duration
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache item</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> Set<TData>(string key, TData data, string[] tags = null);

    /// <summary>
    /// Sets an item to cache store with specified duration
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache item</param>
    /// <param name="duration">Cache expiration duration</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, string[] tags = null);

    /// <summary>
    /// Sets an item to cache store with specified duration
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache item</param>
    /// <param name="duration">Cache expiration duration</param>
    /// <param name="expirationWarningDuration">The duration value any getter client receives expiration warning</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags = null);

    /// <summary>
    /// Sets a string to cache store
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache item</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> SetString(string key, string data, string[] tags = null);

    /// <summary>
    /// Sets a string to cache store
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache item</param>
    /// <param name="duration">Cache expiration duration</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, string[] tags = null);

    /// <summary>
    /// Sets a string to cache store
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache item</param>
    /// <param name="duration">Cache expiration duration</param>
    /// <param name="expirationWarningDuration">The duration value any getter client receives expiration warning</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> SetString(string key, string data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags = null);

    /// <summary>
    /// Sets the binary data to cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache data</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> SetData(string key, byte[] data, string[] tags = null);

    /// <summary>
    /// Sets the binary data to cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache data</param>
    /// <param name="duration">Cache expiration duration</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, string[] tags = null);

    /// <summary>
    /// Sets the binary data to cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="data">Cache data</param>
    /// <param name="duration">Cache expiration duration</param>
    /// <param name="expirationWarningDuration">The duration value any getter client receives expiration warning</param>
    /// <param name="tags">Cache tags</param>
    Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration, TimeSpan expirationWarningDuration, string[] tags = null);

    /// <summary>
    /// Removes a key and value from from
    /// </summary>
    /// <param name="key">Cache key</param>
    Task<HorseResult> Remove(string key);

    /// <summary>
    /// Removes all cache key and values
    /// </summary>
    Task<HorseResult> Purge();

    /// <summary>
    /// Removes all cache keys have the tag
    /// </summary>
    Task<HorseResult> PurgeByTag(string tag);
}