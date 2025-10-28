using System;
using System.IO;
using System.Threading.Tasks;

namespace Horse.Messaging.Plugins.Cache;

/// <summary>
/// Cache Item data
/// </summary>
/// <param name="IsFirstWarningReceiver">True, if first time received the value in warning time range</param>
/// <param name="Item">Cache item</param>
public record PluginCacheItemResult(bool IsFirstWarningReceiver, PluginCacheItem Item);

/// <summary>
/// Server cache operator for plugins
/// </summary>
public interface IPluginCacheRider
{
    /// <summary>
    /// Sets a cache with string value 
    /// </summary>
    Task<PluginCacheOperation> Set(string key, string value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false);

    /// <summary>
    /// Sets a cache with stream value 
    /// </summary>
    Task<PluginCacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false);

    /// <summary>
    /// Gets a cache value by key
    /// </summary>
    Task<PluginCacheItemResult> Get(string key);

    /// <summary>
    /// Gets a integer key value from cache and increases the value by incrementValue atomically. 
    /// </summary>
    Task<PluginCacheItemResult> GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null);

    /// <summary>
    /// Removes a key from cache
    /// </summary>
    Task Remove(string key);

    /// <summary>
    /// Removes all keys with the specified tag
    /// </summary>
    Task PurgeByTag(string tagName);

    /// <summary>
    /// Removes all keys in server
    /// </summary>
    Task Purge();
}