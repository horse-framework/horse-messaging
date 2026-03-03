using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol.Models;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for GetCacheKeys listing and expired key cleanup.
/// </summary>
public class CacheKeysTest
{
    [Fact]
    public async Task GetCacheKeys_ReturnsAllKeys()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        // Purge any existing keys to start clean
        cache.Purge();

        cache.Set("a", "1", TimeSpan.FromMinutes(5));
        cache.Set("b", "2", TimeSpan.FromMinutes(5));
        cache.Set("c", "3", TimeSpan.FromMinutes(5));

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Equal(3, keys.Count);

        var keyNames = keys.Select(k => k.Key).ToList();
        Assert.Contains("a", keyNames);
        Assert.Contains("b", keyNames);
        Assert.Contains("c", keyNames);
    }

    [Fact]
    public async Task GetCacheKeys_EmptyCache_ReturnsEmpty()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        // Purge to ensure empty
        cache.Purge();

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Empty(keys);
    }

    [Fact]
    public async Task GetCacheKeys_ExpiredKeysExcluded()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;

        // Purge any leftovers
        cache.Purge();

        cache.Set("alive", "data", TimeSpan.FromMinutes(5));
        cache.Set("expired", "data", TimeSpan.FromMilliseconds(50));

        await Task.Delay(200);

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.Equal("alive", keys[0].Key);
    }

    [Fact]
    public async Task GetCacheKeys_TagsIncluded()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Purge();
        cache.Set("tagged", "data", TimeSpan.FromMinutes(5), tags: ["tag1", "tag2"]);

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.NotNull(keys[0].Tags);
        Assert.Equal(2, keys[0].Tags.Length);
    }

    [Fact]
    public async Task GetCacheKeys_ExpirationIsInFuture()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Purge();
        cache.Set("future", "data", TimeSpan.FromMinutes(5));

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.True(keys[0].Expiration > 0);
    }

    [Fact]
    public async Task GetCacheKeys_AfterRemove_KeyNotListed()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.Set("stay", "data", TimeSpan.FromMinutes(5));
        cache.Set("gone", "data", TimeSpan.FromMinutes(5));
        cache.Remove("gone");

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.Equal("stay", keys[0].Key);
    }

    [Fact]
    public async Task GetCacheKeys_AfterPurgeByTag_TaggedKeysNotListed()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.Set("tagged-list", "data", TimeSpan.FromMinutes(5), tags: ["grp"]);
        cache.Set("untagged-list", "data", TimeSpan.FromMinutes(5));

        cache.PurgeByTag("grp");

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.Equal("untagged-list", keys[0].Key);
    }

    [Fact]
    public async Task GetCacheKeys_IncrementalItems_AppearInList()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.GetIncremental("inc-list", TimeSpan.FromMinutes(5));

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.Equal("inc-list", keys[0].Key);
    }

    [Fact]
    public async Task GetCacheKeys_WarnCount_Reflected()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.Set("warn-list", "data", TimeSpan.FromSeconds(5), expirationWarning: TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);

        await cache.Get("warn-list"); // trigger warning, warnCount=1
        await cache.Get("warn-list"); // warnCount=2

        List<CacheInformation> keys = cache.GetCacheKeys();
        Assert.Single(keys);
        Assert.Equal(2, keys[0].WarnCount);
    }
}
