using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Server;
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

    [Fact]
    public async Task Persistent_Overwrite_UpdatesFile()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("overwrite-p", "original", TimeSpan.FromMinutes(5), persistent: true);
        long firstSize = new FileInfo(CacheFile(ctx, "overwrite-p")).Length;

        cache.Set("overwrite-p", "updated-with-longer-content", TimeSpan.FromMinutes(5), persistent: true);
        long secondSize = new FileInfo(CacheFile(ctx, "overwrite-p")).Length;

        // File should be updated (different size due to longer content)
        Assert.NotEqual(firstSize, secondSize);

        // Verify value is correct
        var result = await cache.Get("overwrite-p");
        Assert.Equal("updated-with-longer-content", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Persistent_LoadOnRestart_ValueSurvives()
    {
        string dataPath = $"ct-reload-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        try
        {
            // Phase 1: Create server, set persistent item, dispose
            {
                HorseRider rider1 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                rider1.Cache.Set("survive-restart", "persisted-data", TimeSpan.FromMinutes(30), tags: ["t1"], persistent: true);

                string cacheDir = Path.Combine(rider1.Options.DataPath, "Cache");
                Assert.True(Directory.Exists(cacheDir));
                Assert.True(File.Exists(Path.Combine(cacheDir, "survive-restart.hci")));
            }

            // Phase 2: Create new server with same DataPath — item should load from disk
            {
                HorseRider rider2 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                var result = await rider2.Cache.Get("survive-restart");
                Assert.NotNull(result);
                Assert.Equal("persisted-data", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
                Assert.True(result.Item.IsPersistent);
                Assert.Contains("t1", result.Item.Tags);
            }
        }
        finally
        {
            string fullPath = Path.GetFullPath(dataPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }

    [Fact]
    public async Task Persistent_ExpiredOnLoad_FileDeleted()
    {
        string dataPath = $"ct-expire-load-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        try
        {
            // Phase 1: Create server, set persistent item with very short TTL
            {
                HorseRider rider1 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(c =>
                    {
                        c.Options.MinimumDuration = TimeSpan.Zero;
                        c.Options.MaximumDuration = TimeSpan.Zero;
                    })
                    .Build();

                rider1.Cache.Set("will-expire", "temp", TimeSpan.FromMilliseconds(50), persistent: true);

                string cacheDir = Path.Combine(rider1.Options.DataPath, "Cache");
                string filePath = Path.Combine(cacheDir, "will-expire.hci");
                Assert.True(File.Exists(filePath));

                // Wait for expiration
                await Task.Delay(200);
            }

            // Phase 2: New server loads — expired item file should be deleted
            {
                HorseRider rider2 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                var result = await rider2.Cache.Get("will-expire");
                Assert.Null(result);

                string cacheDir = Path.Combine(rider2.Options.DataPath, "Cache");
                string filePath = Path.Combine(cacheDir, "will-expire.hci");
                Assert.False(File.Exists(filePath));
            }
        }
        finally
        {
            string fullPath = Path.GetFullPath(dataPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }

    [Fact]
    public async Task Persistent_MultipleItems_AllLoadOnRestart()
    {
        string dataPath = $"ct-multi-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        try
        {
            {
                HorseRider rider1 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                rider1.Cache.Set("p1", "val1", TimeSpan.FromMinutes(30), persistent: true);
                rider1.Cache.Set("p2", "val2", TimeSpan.FromMinutes(30), persistent: true);
                rider1.Cache.Set("p3", "val3", TimeSpan.FromMinutes(30), persistent: true);
            }

            {
                HorseRider rider2 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                Assert.NotNull(await rider2.Cache.Get("p1"));
                Assert.NotNull(await rider2.Cache.Get("p2"));
                Assert.NotNull(await rider2.Cache.Get("p3"));
                Assert.Equal("val2", Encoding.UTF8.GetString((await rider2.Cache.Get("p2")).Item.Value.ToArray()));
            }
        }
        finally
        {
            string fullPath = Path.GetFullPath(dataPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }

    [Fact]
    public async Task Persistent_Overwrite_ShorterContent_FileCorrectlyTruncated()
    {
        await using var ctx = await CacheTestServer.Create();
        var cache = ctx.Rider.Cache;

        cache.Set("trunc-key", new string('x', 500), TimeSpan.FromMinutes(5), persistent: true);
        long longSize = new FileInfo(CacheFile(ctx, "trunc-key")).Length;

        cache.Set("trunc-key", "short", TimeSpan.FromMinutes(5), persistent: true);
        long shortSize = new FileInfo(CacheFile(ctx, "trunc-key")).Length;

        Assert.True(shortSize < longSize, "File should be truncated when overwritten with shorter content");

        var result = await cache.Get("trunc-key");
        Assert.NotNull(result);
        Assert.Equal("short", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
    }

    [Fact]
    public async Task Persistent_LoadOnRestart_PreservesWarningDate()
    {
        string dataPath = $"ct-warn-persist-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        try
        {
            DateTime warningDateApprox;
            {
                HorseRider rider1 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                warningDateApprox = DateTime.UtcNow.AddMinutes(5);
                rider1.Cache.Set("warn-persist", "data", TimeSpan.FromMinutes(30),
                    expirationWarning: TimeSpan.FromMinutes(5), persistent: true);
            }

            {
                HorseRider rider2 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                var result = await rider2.Cache.Get("warn-persist");
                Assert.NotNull(result);
                Assert.True(result.Item.ExpirationWarning.HasValue, "ExpirationWarning should be loaded from disk");

                double diffSeconds = Math.Abs((result.Item.ExpirationWarning.Value - warningDateApprox).TotalSeconds);
                Assert.True(diffSeconds < 5, $"Warning date drift too large: {diffSeconds}s");
            }
        }
        finally
        {
            string fullPath = Path.GetFullPath(dataPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }

    [Fact]
    public async Task Persistent_LoadOnRestart_MultipleTags_PreservedCorrectly()
    {
        string dataPath = $"ct-tags-persist-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        try
        {
            {
                HorseRider rider1 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                rider1.Cache.Set("multi-tag-p", "data", TimeSpan.FromMinutes(30),
                    tags: ["tag-alpha", "tag-beta", "tag-gamma"], persistent: true);
            }

            {
                HorseRider rider2 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                var result = await rider2.Cache.Get("multi-tag-p");
                Assert.NotNull(result);
                Assert.Equal(3, result.Item.Tags.Length);
                Assert.Contains("tag-alpha", result.Item.Tags);
                Assert.Contains("tag-beta", result.Item.Tags);
                Assert.Contains("tag-gamma", result.Item.Tags);
            }
        }
        finally
        {
            string fullPath = Path.GetFullPath(dataPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }

    [Fact]
    public async Task Persistent_LoadOnRestart_EmptyTags_Preserved()
    {
        string dataPath = $"ct-empty-tags-{Environment.TickCount}-{Random.Shared.Next(0, 100000)}";

        try
        {
            {
                HorseRider rider1 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                rider1.Cache.Set("no-tag-p", "data", TimeSpan.FromMinutes(30), persistent: true);
            }

            {
                HorseRider rider2 = HorseRiderBuilder.Create()
                    .ConfigureOptions(o => o.DataPath = dataPath)
                    .ConfigureCache(_ => { })
                    .Build();

                var result = await rider2.Cache.Get("no-tag-p");
                Assert.NotNull(result);
                Assert.NotNull(result.Item.Tags);
                Assert.Empty(result.Item.Tags);
            }
        }
        finally
        {
            string fullPath = Path.GetFullPath(dataPath);
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
    }
}
