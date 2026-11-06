using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Plugins.Cache;
using Horse.Messaging.Server.Cache;

namespace Horse.Messaging.Server.Plugins;

internal class PluginCacheRider : IPluginCacheRider
{
    private readonly HorseRider _rider;

    internal PluginCacheRider(HorseRider rider)
    {
        _rider = rider;
    }

    public async Task<PluginCacheOperation> Set(string key, string value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        CacheOperation result = await _rider.Cache.SetAsync(key, value, duration, expirationWarning, tags, persistent);
        return new PluginCacheOperation((PluginCacheResult)result.Result, GetCacheItem(result.Item));
    }

    public async Task<PluginCacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        CacheOperation result = await _rider.Cache.SetAsync(key, value, duration, expirationWarning, tags, persistent);
        return new PluginCacheOperation((PluginCacheResult)result.Result, GetCacheItem(result.Item));
    }

    public async Task<PluginCacheItemResult> Get(string key)
    {
        var result = await _rider.Cache.Get(key);
        if (result == null)
            return null;

        return new PluginCacheItemResult(result.IsFirstWarningReceiver, GetCacheItem(result.Item));
    }

    public async Task<PluginCacheItemResult> GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null)
    {
        GetCacheItemResult result = await _rider.Cache.GetIncrementalAsync(key, duration, incrementValue, tags);
        return new PluginCacheItemResult(result.IsFirstWarningReceiver, GetCacheItem(result.Item));
    }

    public Task Remove(string key) => _rider.Cache.RemoveAsync(key);

    public Task PurgeByTag(string tagName) => _rider.Cache.PurgeByTagAsync(tagName);

    public Task Purge() => _rider.Cache.PurgeAsync();

    // Rejected operations (key or size limit) carry no item.
    private static PluginCacheItem GetCacheItem(HorseCacheItem item)
    {
        if (item == null)
            return null;

        return new PluginCacheItem
        {
            Expiration = item.Expiration,
            ExpirationWarnCount = item.ExpirationWarnCount,
            ExpirationWarning = item.ExpirationWarning,
            Key = item.Key,
            Tags = item.Tags,
            Value = item.Value
        };
    }
}