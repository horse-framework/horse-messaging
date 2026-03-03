using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for server-side HorseCache Set/Get/Remove operations.
/// </summary>
public class CacheSetGetTest
{
    [Fact]
    public async Task Set_String_Get_ReturnsValue()
    {
        await using var ctx = await CacheTestServer.Create();

        var cache = ctx.Rider.Cache;
        CacheOperation op = cache.Set("key1", "hello", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);
        Assert.NotNull(op.Item);

        var result = await cache.Get("key1");
        Assert.NotNull(result);
        Assert.NotNull(result.Item);
        Assert.Equal("hello", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Set_MemoryStream_Get_ReturnsValue()
    {
        await using var ctx = await CacheTestServer.Create();

        var cache = ctx.Rider.Cache;
        var data = new MemoryStream("binary-data"u8.ToArray());
        CacheOperation op = cache.Set("key-bin", data, TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);

        var result = await cache.Get("key-bin");
        Assert.NotNull(result);
        Assert.Equal("binary-data", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Set_OverwritesExistingKey()
    {
        await using var ctx = await CacheTestServer.Create();

        var cache = ctx.Rider.Cache;
        cache.Set("overwrite", "first", TimeSpan.FromMinutes(5));
        cache.Set("overwrite", "second", TimeSpan.FromMinutes(5));

        var result = await cache.Get("overwrite");
        Assert.NotNull(result);
        Assert.Equal("second", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Get_NonExistentKey_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create();

        var result = await ctx.Rider.Cache.Get("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task Get_ExpiredKey_ReturnsNull()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        cache.Set("expired", "val", TimeSpan.FromMilliseconds(50));
        await Task.Delay(200);

        var result = await cache.Get("expired");
        Assert.Null(result);
    }

    [Fact]
    public async Task Set_WithTags_TagsPreserved()
    {
        await using var ctx = await CacheTestServer.Create();

        var cache = ctx.Rider.Cache;
        string[] tags = ["tag-a", "tag-b"];
        CacheOperation op = cache.Set("tagged", "data", TimeSpan.FromMinutes(5), tags: tags);
        Assert.Equal(CacheResult.Ok, op.Result);

        var result = await cache.Get("tagged");
        Assert.NotNull(result);
        Assert.Equal(2, result.Item.Tags.Length);
        Assert.Contains("tag-a", result.Item.Tags);
        Assert.Contains("tag-b", result.Item.Tags);
    }

    [Fact]
    public async Task Set_ZeroDuration_UsesDefaultDuration()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.DefaultDuration = TimeSpan.FromMinutes(10);
        });

        var cache = ctx.Rider.Cache;
        cache.Set("default-dur", "val", TimeSpan.Zero);

        var result = await cache.Get("default-dur");
        Assert.NotNull(result);
        // Should expire around 10 minutes from now — just check it's not expired
        Assert.True(result.Item.Expiration > DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task Set_DurationClampedToMinimum()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.FromMinutes(5);
        });

        var cache = ctx.Rider.Cache;
        cache.Set("min-dur", "val", TimeSpan.FromSeconds(10));

        var result = await cache.Get("min-dur");
        Assert.NotNull(result);
        Assert.True(result.Item.Expiration > DateTime.UtcNow.AddMinutes(3));
    }

    [Fact]
    public async Task Set_DurationClampedToMaximum()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumDuration = TimeSpan.FromMinutes(2);
        });

        var cache = ctx.Rider.Cache;
        cache.Set("max-dur", "val", TimeSpan.FromHours(1));

        var result = await cache.Get("max-dur");
        Assert.NotNull(result);
        Assert.True(result.Item.Expiration < DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task CaseInsensitiveKey_SameItem()
    {
        await using var ctx = await CacheTestServer.Create();

        var cache = ctx.Rider.Cache;
        cache.Set("MyKey", "value", TimeSpan.FromMinutes(5));

        var result = await cache.Get("mykey");
        Assert.NotNull(result);
        Assert.Equal("value", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Set_EmptyString_GetReturnsEmpty()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        CacheOperation op = cache.Set("empty-val", "", TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);

        var result = await cache.Get("empty-val");
        Assert.NotNull(result);
        Assert.Equal("", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Set_LargeValue_RoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        byte[] largeData = new byte[8192];
        Random.Shared.NextBytes(largeData);

        CacheOperation op = cache.Set("large", new MemoryStream(largeData), TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);

        var result = await cache.Get("large");
        Assert.NotNull(result);
        Assert.Equal(largeData, result.Item.Value.ToArray());
    }

    [Fact]
    public async Task Set_ReturnedItem_HasCorrectProperties()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        string[] tags = ["tag-x", "tag-y"];
        CacheOperation op = cache.Set("props-key", "props-val", TimeSpan.FromMinutes(10), tags: tags);

        Assert.Equal(CacheResult.Ok, op.Result);
        Assert.NotNull(op.Item);
        Assert.Equal("props-key", op.Item.Key);
        Assert.Equal(tags, op.Item.Tags);
        Assert.True(op.Item.Expiration > DateTime.UtcNow.AddMinutes(8));
        Assert.Equal("props-val", Encoding.UTF8.GetString(op.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Set_Overwrite_UpdatesExpiration()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;

        cache.Set("renew", "first", TimeSpan.FromMinutes(1));
        var first = await cache.Get("renew");
        DateTime firstExpiry = first.Item.Expiration;

        await Task.Delay(50);

        cache.Set("renew", "second", TimeSpan.FromMinutes(10));
        var second = await cache.Get("renew");
        DateTime secondExpiry = second.Item.Expiration;

        Assert.True(secondExpiry > firstExpiry);
    }

    [Fact]
    public async Task Set_Overwrite_UpdatesTags()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("tag-change", "v1", TimeSpan.FromMinutes(5), tags: ["old-tag"]);
        cache.Set("tag-change", "v2", TimeSpan.FromMinutes(5), tags: ["new-tag"]);

        var result = await cache.Get("tag-change");
        Assert.NotNull(result);
        Assert.Single(result.Item.Tags);
        Assert.Contains("new-tag", result.Item.Tags);
    }

    [Fact]
    public async Task ConcurrentSetGet_SameKey_NoException()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() => cache.Set("concurrent", $"val-{idx}", TimeSpan.FromMinutes(5))));
            tasks.Add(Task.Run(async () => await cache.Get("concurrent")));
        }

        await Task.WhenAll(tasks);

        // Final state: key exists with some value
        var result = await cache.Get("concurrent");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Set_NoTags_DefaultsToEmptyArray()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("no-tags", "val", TimeSpan.FromMinutes(5));

        var result = await cache.Get("no-tags");
        Assert.NotNull(result);
        Assert.NotNull(result.Item.Tags);
        Assert.Empty(result.Item.Tags);
    }

    [Fact]
    public async Task Set_MemoryStreamNonExposable_GetReturnsValue()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        byte[] raw = Encoding.UTF8.GetBytes("non-exposable-data");
        var ms = new MemoryStream(raw, 0, raw.Length, writable: false, publiclyVisible: false);

        CacheOperation op = cache.Set("non-exp", ms, TimeSpan.FromMinutes(5));
        Assert.Equal(CacheResult.Ok, op.Result);

        var result2 = await cache.Get("non-exp");
        Assert.NotNull(result2);
        Assert.Equal("non-exposable-data", Encoding.UTF8.GetString(result2.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Get_Expired_RemovesFromDictionary()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        cache.Set("exp-dict", "val", TimeSpan.FromMilliseconds(50));
        await Task.Delay(200);

        Assert.Null(await cache.Get("exp-dict"));
        Assert.Null(await cache.Get("exp-dict"));

        var keys = cache.GetCacheKeys();
        Assert.DoesNotContain(keys, k => k.Key == "exp-dict");
    }

    [Fact]
    public async Task Set_MultipleSequentialOverwrites_LastValueWins()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        for (int i = 0; i < 20; i++)
            cache.Set("rapid", $"v{i}", TimeSpan.FromMinutes(5));

        var result3 = await cache.Get("rapid");
        Assert.NotNull(result3);
        Assert.Equal("v19", Encoding.UTF8.GetString(result3.Item.Value.ToArray()));
    }
}
