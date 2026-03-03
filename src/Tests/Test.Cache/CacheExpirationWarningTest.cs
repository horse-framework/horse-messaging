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
}

