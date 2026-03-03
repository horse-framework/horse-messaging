using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Tests for persistent cache items: write to disk, survive purge, load on restart.
/// </summary>
public class CachePersistenceTest
{
    private static string CacheDir(CacheTestContext ctx) => Path.Combine(ctx.Rider.Options.DataPath, "Cache");
    private static string CacheFile(CacheTestContext ctx, string key) => Path.Combine(CacheDir(ctx), $"{key}.hci");

    [Fact]
    public async Task Persistent_Set_CreatesFileOnDisk()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("persist-key", "persist-val", TimeSpan.FromMinutes(5), persistent: true);

        Assert.True(File.Exists(CacheFile(ctx, "persist-key")), $"Expected file at {CacheFile(ctx, "persist-key")}");
    }

    [Fact]
    public async Task Persistent_Remove_DeletesFile()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("rm-persist", "val", TimeSpan.FromMinutes(5), persistent: true);
        Assert.True(File.Exists(CacheFile(ctx, "rm-persist")));

        cache.Remove("rm-persist");
        Assert.False(File.Exists(CacheFile(ctx, "rm-persist")));
    }

    [Fact]
    public async Task Persistent_Purge_DeletesAllFiles()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("purge-p1", "a", TimeSpan.FromMinutes(5), persistent: true);
        cache.Set("purge-p2", "b", TimeSpan.FromMinutes(5), persistent: true);

        string dir = CacheDir(ctx);
        Assert.True(Directory.Exists(dir));
        Assert.True(Directory.GetFiles(dir).Length >= 2);

        cache.Purge();

        string[] remaining = Directory.Exists(dir) ? Directory.GetFiles(dir) : [];
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Persistent_PurgeByTag_DeletesOnlyTaggedFiles()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("ptag-1", "a", TimeSpan.FromMinutes(5), tags: ["grp"], persistent: true);
        cache.Set("ptag-2", "b", TimeSpan.FromMinutes(5), persistent: true);

        string dir = CacheDir(ctx);
        Assert.True(Directory.Exists(dir));
        Assert.True(Directory.GetFiles(dir).Length >= 2);

        cache.PurgeByTag("grp");

        Assert.False(File.Exists(CacheFile(ctx, "ptag-1")));
        Assert.True(File.Exists(CacheFile(ctx, "ptag-2")));
    }

    [Fact]
    public async Task Persistent_ValueSurvivesRoundTrip()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("roundtrip", "hello-persist", TimeSpan.FromMinutes(30), tags: ["t1", "t2"], persistent: true);

        var result = await cache.Get("roundtrip");
        Assert.NotNull(result);
        Assert.Equal("hello-persist", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
        Assert.True(result.Item.IsPersistent);
        Assert.Equal(2, result.Item.Tags.Length);
    }

    [Fact]
    public async Task Persistent_NonPersistentItem_NoFileCreated()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("no-file", "data", TimeSpan.FromMinutes(5), persistent: false);

        Assert.False(File.Exists(CacheFile(ctx, "no-file")));
    }

    [Fact]
    public async Task Persistent_MixedPersistentAndNonPersistent_OnlyPersistentOnDisk()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("persist-yes", "a", TimeSpan.FromMinutes(5), persistent: true);
        cache.Set("persist-no", "b", TimeSpan.FromMinutes(5), persistent: false);

        Assert.True(File.Exists(CacheFile(ctx, "persist-yes")));
        Assert.False(File.Exists(CacheFile(ctx, "persist-no")));
    }
}
