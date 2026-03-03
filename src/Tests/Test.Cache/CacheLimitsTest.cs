using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for cache limits: MaximumKeys and ValueMaxSize.
/// </summary>
public class CacheLimitsTest
{
    [Fact]
    public async Task MaximumKeys_Enforced()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 3;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        Assert.Equal(CacheResult.Ok, cache.Set("k1", "a", TimeSpan.FromMinutes(5)).Result);
        Assert.Equal(CacheResult.Ok, cache.Set("k2", "b", TimeSpan.FromMinutes(5)).Result);
        Assert.Equal(CacheResult.Ok, cache.Set("k3", "c", TimeSpan.FromMinutes(5)).Result);

        CacheOperation fourth = cache.Set("k4", "d", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.KeyLimit, fourth.Result);
        Assert.Null(fourth.Item);
    }

    [Fact]
    public async Task MaximumKeys_OverwriteExisting_DoesNotCountAsNew()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.Set("k1", "a", TimeSpan.FromMinutes(5));
        cache.Set("k2", "b", TimeSpan.FromMinutes(5));

        // Overwrite k1 — should not trigger limit
        CacheOperation op = cache.Set("k1", "updated", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);

        var result = await cache.Get("k1");
        Assert.Equal("updated", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task MaximumKeys_AfterRemove_SlotFreed()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        var cache = ctx.Rider.Cache;

        cache.Set("k1", "a", TimeSpan.FromMinutes(5));
        cache.Set("k2", "b", TimeSpan.FromMinutes(5));

        // At limit — remove one
        cache.Remove("k1");

        // Now should have room
        CacheOperation op = cache.Set("k3", "c", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);
    }

    [Fact]
    public async Task ValueMaxSize_Enforced()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ValueMaxSize = 100;
        });

        var cache = ctx.Rider.Cache;

        // Small value — ok
        CacheOperation small = cache.Set("small", "hi", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, small.Result);

        // Large value — rejected
        byte[] bigData = new byte[200];
        CacheOperation big = cache.Set("big", new MemoryStream(bigData), TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.ItemSizeLimit, big.Result);
        Assert.Null(big.Item);
    }

    [Fact]
    public async Task ValueMaxSize_ExactBoundary_Accepted()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ValueMaxSize = 50;
        });

        var cache = ctx.Rider.Cache;

        byte[] exactData = new byte[50];
        CacheOperation op = cache.Set("exact", new MemoryStream(exactData), TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);
    }

    [Fact]
    public async Task ValueMaxSize_Zero_Unlimited()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ValueMaxSize = 0; // unlimited
        });

        var cache = ctx.Rider.Cache;

        byte[] bigData = new byte[100_000];
        CacheOperation op = cache.Set("huge", new MemoryStream(bigData), TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);
    }

    [Fact]
    public async Task MaximumKeys_Zero_Unlimited()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 0; // unlimited
        });

        var cache = ctx.Rider.Cache;

        for (int i = 0; i < 100; i++)
        {
            CacheOperation op = cache.Set($"key-{i}", $"val-{i}", TimeSpan.FromMinutes(5));
            Assert.Equal(CacheResult.Ok, op.Result);
        }
    }

    [Fact]
    public async Task ValueMaxSize_OneByteOver_Rejected()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ValueMaxSize = 50;
        });

        var cache = ctx.Rider.Cache;

        byte[] data = new byte[51]; // one byte over limit
        CacheOperation op = cache.Set("over-by-one", new MemoryStream(data), TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.ItemSizeLimit, op.Result);
    }

    [Fact]
    public async Task MaximumKeys_ExpiredKeyFreesSlot()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.Set("short-lived", "a", TimeSpan.FromMilliseconds(50));
        cache.Set("long-lived", "b", TimeSpan.FromMinutes(5));

        await Task.Delay(200);

        // short-lived is expired but still in dictionary until cleanup
        // Try to get it — this triggers removal via Get
        Assert.Null(await cache.Get("short-lived"));

        // Now there should be room for a new key
        CacheOperation op = cache.Set("new-key", "c", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);
    }

    [Fact]
    public async Task CombinedLimits_BothApply()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 5;
            o.ValueMaxSize = 100;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        // Small value accepted
        Assert.Equal(CacheResult.Ok, cache.Set("ok", "small", TimeSpan.FromMinutes(5)).Result);

        // Large value rejected (even though key count < limit)
        byte[] big = new byte[200];
        Assert.Equal(CacheResult.ItemSizeLimit, cache.Set("big", new MemoryStream(big), TimeSpan.FromMinutes(5)).Result);
    }
}

