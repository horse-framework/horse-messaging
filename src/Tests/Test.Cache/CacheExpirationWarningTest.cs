using System;
using System.Threading.Tasks;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for cache expiration warning mechanism.
/// When ExpirationWarning time is reached, the first getter receives IsFirstWarningReceiver=true.
/// </summary>
public class CacheExpirationWarningTest
{
    [Fact]
    public async Task ExpirationWarning_BeforeWarningTime_NoWarning()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ExpirationWarningIsEnabled = false;
        });

        var cache = ctx.Rider.Cache;
        cache.Set("no-warn", "val", TimeSpan.FromMinutes(5));

        var result = await cache.Get("no-warn");
        Assert.NotNull(result);
        Assert.False(result.IsFirstWarningReceiver);
    }

    [Fact]
    public async Task ExpirationWarning_AfterWarningTime_FirstReceiverTrue()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;

        // 2s expiration, warning at 100ms (so warning triggers after 100ms)
        cache.Set("warn-key", "val", TimeSpan.FromSeconds(2), expirationWarning: TimeSpan.FromMilliseconds(100));

        await Task.Delay(200);

        var first = await cache.Get("warn-key");
        Assert.NotNull(first);
        Assert.True(first.IsFirstWarningReceiver);

        // Second get — not first warning receiver
        var second = await cache.Get("warn-key");
        Assert.NotNull(second);
        Assert.False(second.IsFirstWarningReceiver);
    }

    [Fact]
    public async Task ExpirationWarning_WarnCountIncrements()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;

        cache.Set("warn-cnt", "val", TimeSpan.FromSeconds(3), expirationWarning: TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);

        await cache.Get("warn-cnt"); // warnCount 1
        await cache.Get("warn-cnt"); // warnCount 2
        var result = await cache.Get("warn-cnt"); // warnCount 3

        Assert.NotNull(result);
        Assert.Equal(3, result.Item.ExpirationWarnCount);
    }

    [Fact]
    public async Task ExpirationWarning_DefaultWarning_Applied()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ExpirationWarningIsEnabled = true;
            o.DefaultExpirationWarning = TimeSpan.FromMilliseconds(50);
            o.DefaultDuration = TimeSpan.FromSeconds(5);
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        cache.Set("default-warn", "val", TimeSpan.FromSeconds(5));

        await Task.Delay(150);

        var result = await cache.Get("default-warn");
        Assert.NotNull(result);
        Assert.True(result.IsFirstWarningReceiver);
    }

    [Fact]
    public async Task ExpirationWarning_Overwrite_ResetsWarnCount()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        cache.Set("warn-reset", "v1", TimeSpan.FromSeconds(3), expirationWarning: TimeSpan.FromMilliseconds(50));

        await Task.Delay(150);

        // Trigger warning
        var warned = await cache.Get("warn-reset");
        Assert.NotNull(warned);
        Assert.True(warned.IsFirstWarningReceiver);
        Assert.Equal(1, warned.Item.ExpirationWarnCount);

        // Overwrite same key - should reset
        cache.Set("warn-reset", "v2", TimeSpan.FromSeconds(3), expirationWarning: TimeSpan.FromMinutes(10));

        var fresh = await cache.Get("warn-reset");
        Assert.NotNull(fresh);
        Assert.False(fresh.IsFirstWarningReceiver);
        Assert.Equal(0, fresh.Item.ExpirationWarnCount);
    }

    [Fact]
    public async Task ExpirationWarning_Disabled_NeverTriggersWarning()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ExpirationWarningIsEnabled = false;
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        // When warning is disabled but no explicit expirationWarning passed,
        // the Set method sets ExpirationWarning = UtcNow + duration (same as expiration),
        // which means warning would be == expiration. 
        // Key won't be in warning state until it's also expired.
        cache.Set("no-warn-item", "val", TimeSpan.FromSeconds(5));

        // The item's ExpirationWarning should be set to same as Expiration
        var result = await cache.Get("no-warn-item");
        Assert.NotNull(result);
        Assert.False(result.IsFirstWarningReceiver);
    }

    [Fact]
    public async Task ExpirationWarning_ExplicitWarning_OverridesDefault()
    {
        await using var ctx = await CacheTestServer.Create(o =>
        {
            o.ExpirationWarningIsEnabled = true;
            o.DefaultExpirationWarning = TimeSpan.FromMinutes(10);
            o.MinimumDuration = TimeSpan.Zero;
            o.MaximumDuration = TimeSpan.Zero;
        });

        var cache = ctx.Rider.Cache;
        // Set with explicit 50ms warning (overrides default 10min)
        cache.Set("explicit-warn", "val", TimeSpan.FromSeconds(5), expirationWarning: TimeSpan.FromMilliseconds(50));

        await Task.Delay(150);

        var result = await cache.Get("explicit-warn");
        Assert.NotNull(result);
        Assert.True(result.IsFirstWarningReceiver);
    }
}

