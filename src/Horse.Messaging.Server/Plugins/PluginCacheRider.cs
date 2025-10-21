using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
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

    public async Task<PluginCacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null)
    {
        CacheOperation result = await _rider.Cache.Set(key, value, duration, expirationWarning, tags);
        return new PluginCacheOperation((PluginCacheResult)result.Result, GetCacheItem(result.Item));
    }

    public async Task<PluginCacheItemResult> Get(string key)
    {
        var result = await _rider.Cache.Get(key);
        return new PluginCacheItemResult(result.IsFirstWarningReceiver, GetCacheItem(result.Item));
    }

    public async Task<PluginCacheItemResult> GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null)
    {
        var result = await _rider.Cache.GetIncremental(key, duration, incrementValue, tags);
        return new PluginCacheItemResult(result.IsFirstWarningReceiver, GetCacheItem(result.Item));
    }

    public Task Remove(string key)
    {
        return _rider.Cache.Remove(key);
    }

    public Task PurgeByTag(string tagName)
    {
        return _rider.Cache.PurgeByTag(tagName);
    }

    public Task Purge()
    {
        return _rider.Cache.Purge();
    }

    private static PluginCacheItem GetCacheItem(HorseCacheItem item)
    {
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