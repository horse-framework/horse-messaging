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

    public Task<PluginCacheOperation> Set(string key, string value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        CacheOperation result = _rider.Cache.Set(key, value, duration, expirationWarning, tags, persistent);
        PluginCacheOperation operation = new PluginCacheOperation((PluginCacheResult)result.Result, GetCacheItem(result.Item));
        return Task.FromResult(operation);
    }

    public Task<PluginCacheOperation> Set(string key, MemoryStream value, TimeSpan duration, TimeSpan? expirationWarning = null, string[] tags = null, bool persistent = false)
    {
        CacheOperation result = _rider.Cache.Set(key, value, duration, expirationWarning, tags, persistent);
        PluginCacheOperation operation = new PluginCacheOperation((PluginCacheResult)result.Result, GetCacheItem(result.Item));
        return Task.FromResult(operation); 
    }

    public async Task<PluginCacheItemResult> Get(string key)
    {
        var result = await _rider.Cache.Get(key);
        if (result == null)
            return null;

        return new PluginCacheItemResult(result.IsFirstWarningReceiver, GetCacheItem(result.Item));
    }

    public Task<PluginCacheItemResult> GetIncremental(string key, TimeSpan duration, int incrementValue = 1, string[] tags = null)
    {
        var result = _rider.Cache.GetIncremental(key, duration, incrementValue, tags);
        if (result == null)
            return null;

        var itemResult = new PluginCacheItemResult(result.IsFirstWarningReceiver, GetCacheItem(result.Item));
        return Task.FromResult(itemResult);
    }

    public Task Remove(string key)
    {
        _rider.Cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task PurgeByTag(string tagName)
    {
        _rider.Cache.PurgeByTag(tagName);
        return Task.CompletedTask;
    }

    public Task Purge()
    {
        _rider.Cache.Purge();
        return Task.CompletedTask;
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