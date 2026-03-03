using System;
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
}

