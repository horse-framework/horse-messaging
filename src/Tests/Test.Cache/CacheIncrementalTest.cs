using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for GetIncremental: auto-create, increment, expiration reset.
/// </summary>
public class CacheIncrementalTest
{
    [Fact]
    public async Task GetIncremental_NewKey_CreatesWithInitialValue()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        var result = cache.GetIncremental("counter", TimeSpan.FromMinutes(5));
        Assert.NotNull(result);
        Assert.NotNull(result.Item);

        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task GetIncremental_ExistingKey_Increments()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("cnt", TimeSpan.FromMinutes(5));
        cache.GetIncremental("cnt", TimeSpan.FromMinutes(5));
        var result = cache.GetIncremental("cnt", TimeSpan.FromMinutes(5));

        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(3, value);
    }

    [Fact]
    public async Task GetIncremental_CustomIncrementValue()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("step5", TimeSpan.FromMinutes(5), 5);
        var result = cache.GetIncremental("step5", TimeSpan.FromMinutes(5), 5);

        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(10, value);
    }

    [Fact]
    public async Task GetIncremental_ExpiredKey_ResetsToInitialValue()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;

        cache.GetIncremental("expire-cnt", TimeSpan.FromMilliseconds(50));
        cache.GetIncremental("expire-cnt", TimeSpan.FromMilliseconds(50));
        // Value is now 2

        await Task.Delay(200);

        // Expired — should reset to 1
        var result = cache.GetIncremental("expire-cnt", TimeSpan.FromMinutes(5));
        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task GetIncremental_WithTags_TagsPreserved()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        var result = cache.GetIncremental("tagged-cnt", TimeSpan.FromMinutes(5), 1, ["rate-limit"]);
        Assert.NotNull(result.Item.Tags);
        Assert.Contains("rate-limit", result.Item.Tags);
    }

    [Fact]
    public async Task GetIncremental_MultipleKeys_Independent()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("a", TimeSpan.FromMinutes(5));
        cache.GetIncremental("a", TimeSpan.FromMinutes(5));
        cache.GetIncremental("b", TimeSpan.FromMinutes(5));

        int valA = BitConverter.ToInt32(cache.GetIncremental("a", TimeSpan.FromMinutes(5)).Item.Value.ToArray());
        int valB = BitConverter.ToInt32(cache.GetIncremental("b", TimeSpan.FromMinutes(5)).Item.Value.ToArray());

        Assert.Equal(3, valA);
        Assert.Equal(2, valB);
    }
}

