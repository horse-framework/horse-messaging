using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Plugins.Cache;

namespace Horse.Messaging.Plugins;

/// <summary>
/// Cache Item data
/// </summary>
/// <param name="IsFirstWarningReceiver">True, if first time received the value in warning time range</param>
/// <param name="Item">Cache item</param>
public record PluginCacheItemResult(bool IsFirstWarningReceiver, PluginCacheItem Item);

public interface IPluginCacheRider
{
    Task<PluginCacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null);
    Task<PluginCacheItemResult> Get(string key);
    Task<PluginCacheItemResult> GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null);
    
    Task Remove(string key);
    Task PurgeByTag(string tagName);
    Task Purge();
}