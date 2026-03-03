using System;
using System.Collections.Generic;
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

    [Fact]
    public async Task GetIncremental_RespectsMaximumKeys()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.GetIncremental("cnt-1", TimeSpan.FromMinutes(5));
        cache.GetIncremental("cnt-2", TimeSpan.FromMinutes(5));

        // Third key should fail due to limit (GetIncremental calls Set internally)
        var result = cache.GetIncremental("cnt-3", TimeSpan.FromMinutes(5));
        // The Set inside GetIncremental returns KeyLimit, and operation.Item is null
        Assert.Null(result.Item);
    }

    [Fact]
    public async Task GetIncremental_ExistingKey_DoesNotCountAgainstKeyLimit()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MaximumKeys = 2;
        });

        var cache = ctx.Rider.Cache;
        cache.Purge();

        cache.GetIncremental("cnt-a", TimeSpan.FromMinutes(5));
        cache.GetIncremental("cnt-b", TimeSpan.FromMinutes(5));

        // Incrementing existing key should not fail
        var result = cache.GetIncremental("cnt-a", TimeSpan.FromMinutes(5));
        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(2, value);
    }

    [Fact]
    public async Task GetIncremental_NegativeIncrement_Decrements()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("dec", TimeSpan.FromMinutes(5), 10); // start at 10
        cache.GetIncremental("dec", TimeSpan.FromMinutes(5), -3); // 10 + (-3) = 7

        var result = cache.GetIncremental("dec", TimeSpan.FromMinutes(5), 0); // 7 + 0 = 7
        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(7, value);
    }

    [Fact]
    public async Task GetIncremental_AfterPurge_StartsFromInitial()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("purge-cnt", TimeSpan.FromMinutes(5));
        cache.GetIncremental("purge-cnt", TimeSpan.FromMinutes(5));
        cache.GetIncremental("purge-cnt", TimeSpan.FromMinutes(5)); // val = 3

        cache.Purge();

        var result = cache.GetIncremental("purge-cnt", TimeSpan.FromMinutes(5));
        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(1, value); // reset
    }

    [Fact]
    public async Task GetIncremental_AfterRemove_StartsFromInitial()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("rm-cnt", TimeSpan.FromMinutes(5));
        cache.GetIncremental("rm-cnt", TimeSpan.FromMinutes(5)); // val = 2

        cache.Remove("rm-cnt");

        var result = cache.GetIncremental("rm-cnt", TimeSpan.FromMinutes(5));
        int value = BitConverter.ToInt32(result.Item.Value.ToArray());
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task GetIncremental_ConcurrentIncrements_Consistent()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("conc-inc", TimeSpan.FromMinutes(5));

        int parallelCount = 50;
        var tasks = new List<Task>();
        for (int i = 0; i < parallelCount; i++)
            tasks.Add(Task.Run(() => cache.GetIncremental("conc-inc", TimeSpan.FromMinutes(5))));

        await Task.WhenAll(tasks);

        var result2 = cache.GetIncremental("conc-inc", TimeSpan.FromMinutes(5));
        int finalVal = BitConverter.ToInt32(result2.Item.Value.ToArray());

        Assert.Equal(parallelCount + 2, finalVal);
    }

    [Fact]
    public async Task GetIncremental_PurgeByTag_ResetsTaggedCounter()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("tag-cnt", TimeSpan.FromMinutes(5), 1, ["counter-group"]);
        cache.GetIncremental("tag-cnt", TimeSpan.FromMinutes(5));

        cache.PurgeByTag("counter-group");

        var result3 = cache.GetIncremental("tag-cnt", TimeSpan.FromMinutes(5));
        int val = BitConverter.ToInt32(result3.Item.Value.ToArray());
        Assert.Equal(1, val);
    }

    [Fact]
    public async Task GetIncremental_CaseInsensitiveKey()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.GetIncremental("Counter", TimeSpan.FromMinutes(5));
        cache.GetIncremental("counter", TimeSpan.FromMinutes(5));
        cache.GetIncremental("COUNTER", TimeSpan.FromMinutes(5));

        var result4 = cache.GetIncremental("counter", TimeSpan.FromMinutes(5));
        int val2 = BitConverter.ToInt32(result4.Item.Value.ToArray());
        Assert.Equal(4, val2);
    }
}
