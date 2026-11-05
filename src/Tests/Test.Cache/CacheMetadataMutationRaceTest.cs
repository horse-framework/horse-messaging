using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

public class CacheMetadataMutationRaceTest
{
    [Fact]
    public void Retag_AndIncrement_KeepCurrentTagMembershipConsistent()
    {
        using var context = new LocalCache();
        var original = context.Cache.Set("counter", new MemoryStream(BitConverter.GetBytes(7)), TimeSpan.FromMinutes(5), tags: ["old"]).Item;
        RunWhileSetterWaitsForPartition(context.Cache, "counter", () => original.Tags = ["new"],
            () => context.Cache.GetIncremental("counter", TimeSpan.FromMinutes(5)));

        Assert.True(context.Cache.TryGet("counter", out var current));
        string actualTag = Assert.Single(current.Item.Tags);
        string otherTag = actualTag == "old" ? "new" : "old";
        context.Cache.PurgeByTag(otherTag);
        Assert.True(context.Cache.TryGet("counter", out _));
        context.Cache.PurgeByTag(actualTag);
        Assert.False(context.Cache.TryGet("counter", out _));
    }

    [Fact]
    public void ExpirationSetter_WaitingBehindIncrement_CannotResetNewVersion()
    {
        using var context = new LocalCache();
        var original = context.Cache.Set("counter", new MemoryStream(BitConverter.GetBytes(7)), TimeSpan.FromMinutes(5)).Item;
        DateTime expiration = original.Expiration;
        RunWhileSetterWaitsForPartition(context.Cache, "counter", () => original.Expiration = DateTime.UtcNow.AddSeconds(-1),
            () => context.Cache.GetIncremental("counter", TimeSpan.FromHours(1)));

        Assert.True(context.Cache.TryGet("counter", out var current));
        Assert.Equal(8, BitConverter.ToInt32(current.Item.Value.ToArray()));
        Assert.Equal(expiration, current.Item.Expiration);
    }

    private static void RunWhileSetterWaitsForPartition(HorseCache cache, string key, Action setter, Action mutation)
    {
        object partition = typeof(HorseCache).GetMethod("GetPartition", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache, [key]);
        object gate = partition.GetType().GetField("Gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(partition);
        using var began = new ManualResetEventSlim();
        Exception failure = null;
        var worker = new Thread(() =>
        {
            began.Set();
            try { setter(); }
            catch (Exception error) { failure = error; }
        });
        try
        {
            lock (gate)
            {
                worker.Start();
                Assert.True(began.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(() => (worker.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)));
                mutation();
            }
        }
        finally
        {
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        }
        Assert.Null(failure);
    }

    private sealed class LocalCache : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "horse-cache-metadata-" + Guid.NewGuid().ToString("N"));
        public HorseCache Cache { get; }

        public LocalCache() => Cache = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = _directory).Build().Cache;

        public void Dispose()
        {
            Cache.Dispose();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
