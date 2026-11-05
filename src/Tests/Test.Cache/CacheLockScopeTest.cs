using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Plugins.Cache;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Plugins;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Mutations commit under the partition gate while persistence waits, events and
/// bulk expiry work happen outside it or in bounded slices.
/// </summary>
public class CacheLockScopeTest
{
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task AsyncMutations_CommitBeforeReturning_AndCompleteWhenPersisted()
    {
        await using var scope = new CacheScope();
        HorseCache cache = scope.Cache;
        cache.Set("removed", "value", Duration, persistent: true);
        cache.Set("counter", new MemoryStream(BitConverter.GetBytes(5)), Duration, persistent: true);
        cache.Set("tagged", "value", Duration, tags: ["group"], persistent: true);

        using BlockingStream gate = new();
        Task blocker = scope.Writer.WriteAsync(new HorseCacheItem { Key = "blocker", Expiration = DateTime.UtcNow.Add(Duration), Value = gate });
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        ValueTask<CacheOperation> set = cache.SetAsync("added", "value", Duration, persistent: true);
        ValueTask<GetCacheItemResult> increment = cache.GetIncrementalAsync("counter", Duration);
        Task remove = cache.RemoveAsync("removed");
        Task purge = cache.PurgeByTagAsync("group");

        // The memory state is final although every file operation is still queued.
        Assert.False(set.IsCompleted);
        Assert.False(increment.IsCompleted);
        Assert.False(remove.IsCompleted);
        Assert.False(purge.IsCompleted);
        Assert.True(cache.TryGet("added", out _));
        Assert.True(cache.TryGet("counter", out var counter));
        Assert.Equal(6, BitConverter.ToInt32(counter.Item.Value.ToArray()));
        Assert.False(cache.TryGet("removed", out _));
        Assert.False(cache.TryGet("tagged", out _));

        // Another key in the same partition is not held up by the queued IO.
        string neighbour = scope.KeysInPartitionOf("counter", 1)[0];
        Assert.True(Task.Run(() => cache.Set(neighbour, "value", Duration)).Wait(TimeSpan.FromSeconds(5)));

        gate.Release.Set();
        await Task.WhenAll(blocker, set.AsTask(), increment.AsTask(), remove, purge).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(6, BitConverter.ToInt32((await increment).Item.Value.ToArray()));

        await using var reloaded = scope.Reload();
        Assert.True(reloaded.TryGet("added", out _));
        Assert.True(reloaded.TryGet("counter", out var persisted));
        Assert.Equal(6, BitConverter.ToInt32(persisted.Item.Value.ToArray()));
        Assert.False(reloaded.TryGet("removed", out _));
        Assert.False(reloaded.TryGet("tagged", out _));
    }

    [Fact]
    public async Task PurgeAsync_ClearsMemoryAtOnce_AndFilesWhenCompleted()
    {
        await using var scope = new CacheScope();
        scope.Cache.Set("first", "value", Duration, persistent: true);
        scope.Cache.Set("second", "value", Duration, persistent: true);

        Task purge = scope.Cache.PurgeAsync();

        Assert.Equal(0, scope.Cache.Count);
        await purge.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Empty(Directory.GetFiles(scope.CacheDirectory, "*.hci"));
    }

    [Fact]
    public async Task TimerPass_RemovesAtMostOneSlicePerPartition()
    {
        await using var scope = new CacheScope();
        scope.StopTimer();
        List<string> keys = scope.KeysInPartitionOf("anchor", 1500);
        foreach (string key in keys)
            scope.Cache.Set(key, "value", TimeSpan.FromMilliseconds(50));
        await Task.Delay(200);

        scope.RunTimerPass();
        Assert.Equal(1500 - 1024, scope.Cache.Count);

        scope.RunTimerPass();
        Assert.Equal(0, scope.Cache.Count);
    }

    [Fact]
    public async Task KeyLimit_RemovesEveryExpiredEntryBeforeRejecting()
    {
        await using var scope = new CacheScope(options => options.MaximumKeys = 1500);
        scope.StopTimer();
        foreach (string key in scope.KeysInPartitionOf("anchor", 1500))
            scope.Cache.Set(key, "value", TimeSpan.FromMilliseconds(50));
        await Task.Delay(200);

        CacheOperation operation = scope.Cache.Set("fresh", "value", Duration);

        Assert.Equal(CacheResult.Ok, operation.Result);
        Assert.Equal(1, scope.Cache.Count);
    }

    [Fact]
    public async Task PluginSet_RejectedByKeyLimit_ReturnsResultWithoutItem()
    {
        await using var scope = new CacheScope(options => options.MaximumKeys = 1);
        IPluginCacheRider plugin = new PluginCacheRider(scope.Rider);
        await plugin.Set("first", "value", Duration);

        PluginCacheOperation rejected = await plugin.Set("second", "value", Duration);

        Assert.Equal(PluginCacheResult.KeyLimit, rejected.Result);
        Assert.Null(rejected.Item);
    }

    private sealed class CacheScope : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "horse-cache-lock-" + Guid.NewGuid().ToString("N"));
        private readonly Action<HorseCacheOptions> _configure;
        private readonly List<HorseCache> _reloaded = new();

        internal HorseRider Rider { get; }
        internal HorseCache Cache => Rider.Cache;
        internal string CacheDirectory => Path.Combine(_path, "Cache");

        internal CachePersistenceWriter Writer =>
            (CachePersistenceWriter)typeof(HorseCache).GetField("_persistence", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Cache);

        internal CacheScope(Action<HorseCacheOptions> configure = null)
        {
            _configure = configure;
            Rider = Build();
        }

        private HorseRider Build() => HorseRiderBuilder.Create()
            .ConfigureOptions(o => o.DataPath = _path)
            .ConfigureCache(c => _configure?.Invoke(c.Options))
            .Build();

        internal HorseCache Reload()
        {
            Cache.Dispose();
            HorseCache cache = Build().Cache;
            _reloaded.Add(cache);
            return cache;
        }

        internal void StopTimer()
        {
            FieldInfo field = typeof(HorseCache).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((Timer)field.GetValue(Cache))?.Dispose();
            field.SetValue(Cache, null);
        }

        internal void RunTimerPass()
            => typeof(HorseCache).GetMethod("RemoveExpiredKeys", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Cache, null);

        // Mirrors HorseCache.GetPartition so tests can fill one partition.
        internal List<string> KeysInPartitionOf(string anchor, int count)
        {
            static uint Partition(string key) => (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(key) % 64;
            uint target = Partition(anchor);
            List<string> keys = new(count);
            for (int i = 0; keys.Count < count; i++)
            {
                string key = anchor + "-" + i;
                if (Partition(key) == target)
                    keys.Add(key);
            }
            return keys;
        }

        public async ValueTask DisposeAsync()
        {
            await Cache.DisposeAsync();
            foreach (HorseCache cache in _reloaded)
                await cache.DisposeAsync();
            if (Directory.Exists(_path))
                Directory.Delete(_path, true);
        }
    }

    private sealed class BlockingStream : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();

        // A hidden buffer makes the writer call WriteTo, where the test holds it.
        internal BlockingStream() : base(Encoding.UTF8.GetBytes("gate")) { }

        public override void WriteTo(Stream stream)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The test did not release the persistence writer.");
            base.WriteTo(stream);
        }

        protected override void Dispose(bool disposing)
        {
            Release.Set();
            if (disposing)
                Release.Dispose();
            base.Dispose(disposing);
        }
    }
}
