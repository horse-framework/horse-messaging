using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Test.Cache;

public class CacheServerRestartTest
{
    [Fact]
    public async Task SameServer_Restart_PreservesCacheAndAcceptsNewWrites()
    {
        await using var context = await CacheTestServer.Create();
        var cache = context.Rider.Cache;
        cache.Set("before", "retained", TimeSpan.FromMinutes(10));

        await context.Rider.Server.StopAsync();
        await context.Rider.Server.StartAsync();

        Assert.Equal("retained", Encoding.UTF8.GetString((await cache.Get("before")).Item.Value.ToArray()));
        cache.Set("after", "accepted", TimeSpan.FromMinutes(10));
        Assert.Equal("accepted", Encoding.UTF8.GetString((await cache.Get("after")).Item.Value.ToArray()));
    }

    [Fact]
    public async Task Restart_ResumesExpirationMaintenance()
    {
        await using var context = await CacheTestServer.Create();
        var cache = context.Rider.Cache;
        cache.Set("long", "retained", TimeSpan.FromMinutes(10));

        await context.Rider.Server.StopAsync();
        cache.Set("short", "value", TimeSpan.FromMilliseconds(150));
        await Task.Delay(200);
        Assert.Equal(2, cache.Count);
        await context.Rider.Server.StartAsync();

        Stopwatch timeout = Stopwatch.StartNew();
        while (cache.Count > 1 && timeout.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(30);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("long", out _));
        Assert.False(cache.TryGet("short", out _));
    }

    [Fact]
    public async Task ServerStop_DrainsAcceptedPersistence_AndRestartCanPersistAgain()
    {
        await using var context = await CacheTestServer.Create();
        var cache = context.Rider.Cache;
        Task[] accepted = Enumerable.Range(0, 1500).Select(i =>
            cache.SetAsync("persistent", "value-" + i, TimeSpan.FromMinutes(10), persistent: true).AsTask()).ToArray();

        await context.Rider.Server.StopAsync();
        try
        {
            Assert.All(accepted, operation => Assert.True(operation.IsCompletedSuccessfully));
            string filename = Path.Combine(context.Rider.Options.DataPath, "Cache", "persistent.hci");
            using (var reader = new BinaryReader(File.OpenRead(filename)))
            {
                Assert.Equal("persistent", reader.ReadString());
                reader.ReadInt64();
                reader.ReadInt32();
                if (reader.ReadBoolean()) reader.ReadInt64();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++) reader.ReadString();
                Assert.Equal("value-1499", Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())));
            }
        }
        finally
        {
            await context.Rider.Server.StartAsync();
        }

        await cache.SetAsync("after", "saved", TimeSpan.FromMinutes(10), persistent: true);
        Assert.True(File.Exists(Path.Combine(context.Rider.Options.DataPath, "Cache", "after.hci")));
    }

    [Fact]
    public async Task ExplicitDispose_RemainsFinalAcrossServerRestart()
    {
        await using var context = await CacheTestServer.Create();
        var cache = context.Rider.Cache;
        await cache.DisposeAsync();

        await context.Rider.Server.StopAsync();
        await context.Rider.Server.StartAsync();

        Assert.Throws<ObjectDisposedException>(() => cache.Set("after", "value", TimeSpan.FromMinutes(10)));
        Assert.False(cache.TryGet("after", out _));
    }
}
