using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for cache Remove, Purge, and PurgeByTag operations.
/// </summary>
public class CacheRemovePurgeTest
{
    [Fact]
    public async Task Remove_ExistingKey_KeyGone()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("rm-key", "data", TimeSpan.FromMinutes(5));
        cache.Remove("rm-key");

        var result = await cache.Get("rm-key");
        Assert.Null(result);
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow()
    {
        // Synchronous — just ensure no exception
        var ctx = CacheTestServer.Create().GetAwaiter().GetResult();
        try
        {
            ctx.Rider.Cache.Remove("not-here");
        }
        finally
        {
            ctx.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    [Fact]
    public async Task Purge_RemovesAllKeys()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("p1", "a", TimeSpan.FromMinutes(5));
        cache.Set("p2", "b", TimeSpan.FromMinutes(5));
        cache.Set("p3", "c", TimeSpan.FromMinutes(5));

        cache.Purge();

        Assert.Null(await cache.Get("p1"));
        Assert.Null(await cache.Get("p2"));
        Assert.Null(await cache.Get("p3"));
    }

    [Fact]
    public async Task Purge_EmptyCache_DoesNotThrow()
    {
        await using var ctx = await CacheTestServer.Create();
        ctx.Rider.Cache.Purge();
        // No exception = pass
    }

    [Fact]
    public async Task PurgeByTag_RemovesOnlyTaggedKeys()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("t1", "a", TimeSpan.FromMinutes(5), tags: ["group-a"]);
        cache.Set("t2", "b", TimeSpan.FromMinutes(5), tags: ["group-a", "group-b"]);
        cache.Set("t3", "c", TimeSpan.FromMinutes(5), tags: ["group-b"]);
        cache.Set("t4", "d", TimeSpan.FromMinutes(5));

        cache.PurgeByTag("group-a");

        Assert.Null(await cache.Get("t1"));
        Assert.Null(await cache.Get("t2"));
        Assert.NotNull(await cache.Get("t3")); // group-b only
        Assert.NotNull(await cache.Get("t4")); // no tags
    }

    [Fact]
    public async Task PurgeByTag_CaseInsensitiveTagMatch()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("ci-tag", "data", TimeSpan.FromMinutes(5), tags: ["MyTag"]);

        cache.PurgeByTag("mytag");

        Assert.Null(await cache.Get("ci-tag"));
    }

    [Fact]
    public async Task PurgeByTag_NonExistentTag_NothingRemoved()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("survive", "data", TimeSpan.FromMinutes(5), tags: ["keep"]);

        cache.PurgeByTag("nonexistent");

        Assert.NotNull(await cache.Get("survive"));
    }

    [Fact]
    public async Task Remove_ThenSet_SameKey_Works()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("cycle", "first", TimeSpan.FromMinutes(5));
        cache.Remove("cycle");

        cache.Set("cycle", "second", TimeSpan.FromMinutes(5));

        var result = await cache.Get("cycle");
        Assert.NotNull(result);
        Assert.Equal("second", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task PurgeByTag_MultipleTagsOnItem_MatchesAny()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("multi-tag", "data", TimeSpan.FromMinutes(5), tags: ["alpha", "beta"]);

        // Purge by second tag only
        cache.PurgeByTag("beta");

        Assert.Null(await cache.Get("multi-tag"));
    }

    [Fact]
    public async Task Purge_FreesSlotForNewKeys()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        var cache = ctx.Rider.Cache;

        cache.Set("a", "1", TimeSpan.FromMinutes(5));
        cache.Set("b", "2", TimeSpan.FromMinutes(5));

        // At limit
        Assert.Equal(CacheResult.KeyLimit, cache.Set("c", "3", TimeSpan.FromMinutes(5)).Result);

        cache.Purge();

        // After purge, slots are free
        Assert.Equal(CacheResult.Ok, cache.Set("c", "3", TimeSpan.FromMinutes(5)).Result);
        Assert.Equal(CacheResult.Ok, cache.Set("d", "4", TimeSpan.FromMinutes(5)).Result);
    }

    [Fact]
    public async Task PurgeByTag_FreesSlotForNewKeys()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        var cache = ctx.Rider.Cache;

        cache.Set("tagged", "1", TimeSpan.FromMinutes(5), tags: ["session"]);
        cache.Set("untagged", "2", TimeSpan.FromMinutes(5));

        Assert.Equal(CacheResult.KeyLimit, cache.Set("new", "3", TimeSpan.FromMinutes(5)).Result);

        cache.PurgeByTag("session");

        Assert.Equal(CacheResult.Ok, cache.Set("new", "3", TimeSpan.FromMinutes(5)).Result);
    }

    [Fact]
    public async Task Remove_CaseInsensitiveKey_Removes()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("CaseKey", "data", TimeSpan.FromMinutes(5));
        cache.Remove("casekey");

        Assert.Null(await cache.Get("CaseKey"));
    }
}

