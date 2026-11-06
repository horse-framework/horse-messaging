using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Server;
using Xunit;

namespace Test.Cache;

public class CacheAsyncPersistenceTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncWrites_AndConcurrentDisposal_DrainLatestValue(bool atomicWrites)
    {
        string path = Path.Combine(Path.GetTempPath(), "horse-cache-drain-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = path)
                .ConfigureCache(c => c.Options.AtomicPersistentWrites = atomicWrites).Build().Cache;
            var writes = new List<Task>();
            for (int i = 0; i < 1500; i++)
                writes.Add(cache.SetAsync("key", "value-" + i, TimeSpan.FromMinutes(10), persistent: true).AsTask());
            Task first = cache.DisposeAsync().AsTask();
            Task second = cache.DisposeAsync().AsTask();
            await Task.WhenAll(writes.Append(first).Append(second));

            await using var reloaded = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = path).Build().Cache;
            Assert.True(reloaded.TryGet("key", out var result));
            Assert.Equal("value-1499", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
            Assert.Empty(Directory.GetFiles(Path.Combine(path, "Cache"), "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task TransientReplacement_AndRemove_DoNotRestoreOldPersistentValue()
    {
        string path = Path.Combine(Path.GetTempPath(), "horse-cache-replace-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var cache = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = path).Build().Cache)
            {
                await cache.SetAsync("key", "old", TimeSpan.FromMinutes(10), persistent: true);
                await cache.SetAsync("KEY", "new", TimeSpan.FromMinutes(10));
                cache.Remove("key");
            }
            await using var reloaded = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = path).Build().Cache;
            Assert.False(reloaded.TryGet("key", out _));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task PersistentCounterAndCaseVariantWrite_SurviveRestart()
    {
        string path = Path.Combine(Path.GetTempPath(), "horse-cache-counter-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var cache = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = path).Build().Cache)
            {
                await cache.SetAsync("counter", new MemoryStream(BitConverter.GetBytes(1)), TimeSpan.FromMinutes(10), persistent: true);
                cache.GetIncremental("COUNTER", TimeSpan.FromMinutes(10), 4);
                await cache.SetAsync("value", "first", TimeSpan.FromMinutes(10), persistent: true);
                await cache.SetAsync("VALUE", "latest", TimeSpan.FromMinutes(10), persistent: true);
                Assert.Single(Directory.GetFiles(Path.Combine(path, "Cache"), "value.hci"));
                Assert.Equal(2, Directory.GetFiles(Path.Combine(path, "Cache"), "*.hci").Length);
            }
            await using var reloaded = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = path).Build().Cache;
            Assert.True(reloaded.TryGet("counter", out var counter));
            Assert.Equal(5, BitConverter.ToInt32(counter.Item.Value.ToArray()));
            Assert.True(reloaded.TryGet("value", out var value));
            Assert.Equal("latest", Encoding.UTF8.GetString(value.Item.Value.ToArray()));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [Fact]
    public async Task PersistentItem_KeyCasingChange_DeletesCanonicalFile()
    {
        await using var context = await CacheTestServer.Create();
        var cache = context.Rider.Cache;
        var stored = cache.Set("canonical", "saved", TimeSpan.FromMinutes(10), persistent: true).Item;
        stored.Key = "CANONICAL";
        cache.Remove("CANONICAL");
        Assert.Empty(Directory.GetFiles(Path.Combine(context.Rider.Options.DataPath, "Cache"), "*.hci"));
    }

    [Fact]
    public async Task ServerStop_DrainsPersistenceAndStopsCache()
    {
        await using var context = await CacheTestServer.Create();
        var cache = context.Rider.Cache;
        var accepted = cache.SetAsync("stop", "saved", TimeSpan.FromMinutes(10), persistent: true).AsTask();
        await context.Rider.Server.StopAsync();
        await accepted;
        Assert.True(File.Exists(Path.Combine(context.DataPath, "Cache", "stop.hci")));
        Assert.Equal(Horse.Messaging.Server.Cache.CacheResult.Ok, cache.Set("after-stop", "value", TimeSpan.FromMinutes(1)).Result);
    }

    [Fact]
    public async Task ConfiguredRider_ServerStop_IsManagedByCache()
    {
        string path = Path.Combine(Path.GetTempPath(), "horse-cache-lifecycle-" + Guid.NewGuid().ToString("N"));
        var options = HorseServerOptions.CreateDefault();
        options.Hosts[0].Port = 0;
        var server = new HorseServer(options);
        var rider = server.UseRider(builder => builder.ConfigureOptions(o => o.DataPath = path));
        try
        {
            await server.StartAsync();
            await rider.Cache.SetAsync("configured", "saved", TimeSpan.FromMinutes(10), persistent: true);
            await server.StopAsync();
            Assert.Equal(Horse.Messaging.Server.Cache.CacheResult.Ok, rider.Cache.Set("after-stop", "value", TimeSpan.FromMinutes(1)).Result);
            Assert.True(File.Exists(Path.Combine(path, "Cache", "configured.hci")));
        }
        finally
        {
            if (server.IsRunning)
                await server.StopAsync();
            await rider.Cache.DisposeAsync();
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
