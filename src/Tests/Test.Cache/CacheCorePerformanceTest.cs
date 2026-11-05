using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

public class CacheCorePerformanceTest
{
    [Fact]
    public void MaximumKeys_ConcurrentNewKeys_ReservesCapacityAtomically()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        cache.Options.MaximumKeys = 4;
        int accepted = 0;
        int rejected = 0;

        Parallel.For(0, 128, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            CacheOperation operation = cache.Set("key-" + i, "value", TimeSpan.FromMinutes(5));
            if (operation.Result == CacheResult.Ok)
                Interlocked.Increment(ref accepted);
            else if (operation.Result == CacheResult.KeyLimit)
                Interlocked.Increment(ref rejected);
        });

        Assert.Equal(4, accepted);
        Assert.Equal(124, rejected);
        Assert.Equal(4, cache.Count);
    }

    [Fact]
    public void Count_TracksOverwriteRemovalTagPurgeAndPurge()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        cache.Options.MaximumKeys = 2;
        Assert.Equal(CacheResult.Ok, cache.Set("a", "first", TimeSpan.FromMinutes(5), tags: ["old"]).Result);
        Assert.Equal(CacheResult.Ok, cache.Set("b", "second", TimeSpan.FromMinutes(5), tags: ["other"]).Result);
        Assert.Equal(2, cache.Count);

        Assert.Equal(CacheResult.Ok, cache.Set("A", "replacement", TimeSpan.FromMinutes(5), tags: ["new"]).Result);
        Assert.Equal(2, cache.Count);
        cache.PurgeByTag("old");
        Assert.True(cache.TryGet("a", out _));
        Assert.Equal(2, cache.Count);

        cache.PurgeByTag("NEW");
        Assert.False(cache.TryGet("a", out _));
        Assert.Equal(1, cache.Count);
        cache.Remove("b");
        cache.Remove("b");
        Assert.Equal(0, cache.Count);

        cache.Set("a", "again", TimeSpan.FromMinutes(5));
        cache.Set("b", "again", TimeSpan.FromMinutes(5));
        cache.Purge();
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, ExpirationEntryCount(cache));
        Assert.False(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
    }

    [Fact]
    public void MaximumKeys_ExpiredEntryIsReclaimedForNewKey()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        cache.Options.MaximumKeys = 1;
        cache.Set("expired", "old", TimeSpan.FromSeconds(-1));

        CacheOperation operation = cache.Set("new", "fresh", TimeSpan.FromMinutes(5));

        Assert.Equal(CacheResult.Ok, operation.Result);
        Assert.Equal(1, cache.Count);
        Assert.False(cache.TryGet("expired", out _));
        Assert.True(cache.TryGet("new", out _));
    }

    [Fact]
    public void ExpirationCleanup_DoesNotRemoveReplacementOfExpiredEntry()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        cache.Set("key", "expired", TimeSpan.FromSeconds(-1));
        cache.Set("KEY", "fresh", TimeSpan.FromMinutes(5));

        RunExpirationCleanup(cache);

        Assert.True(cache.TryGet("key", out CacheReadResult result));
        Assert.Equal("fresh", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
        Assert.Equal(1, cache.Count);
        Assert.Equal(1, ExpirationEntryCount(cache));
    }

    [Fact]
    public void ExpirationIndex_RepeatedOverwriteKeepsOnlyCurrentEntry()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        for (int i = 0; i < 10000; i++)
            cache.Set(i % 2 == 0 ? "key" : "KEY", "value", TimeSpan.FromDays(i % 2 == 0 ? 1 : 2));

        Assert.Equal(1, cache.Count);
        Assert.Equal(1, ExpirationEntryCount(cache));
        cache.Remove("key");
        Assert.Equal(0, ExpirationEntryCount(cache));
    }

    [Fact]
    public void TryGet_WarmedSuccessfulLookups_AllocateNoManagedMemory()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        cache.Options.ExpirationWarningIsEnabled = false;
        cache.Set("key", "value", TimeSpan.FromMinutes(5));
        for (int i = 0; i < 10000; i++)
            cache.TryGet("key", out _);

        bool allFound = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20000; i++)
            allFound &= cache.TryGet("key", out _);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allFound);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Set_OwnsPayloadAndDoesNotRetainCallerBuffer()
    {
        using var context = new CacheContext();
        using var input = new MemoryStream(100000);
        input.Write(Encoding.UTF8.GetBytes("original"));
        context.Cache.Set("key", input, TimeSpan.FromMinutes(5));

        input.Position = 0;
        input.Write(Encoding.UTF8.GetBytes("CHANGED!"));
        Assert.True(context.Cache.TryGet("key", out CacheReadResult result));
        Assert.Equal("original", Encoding.UTF8.GetString(result.Item.Value.ToArray()));
        Assert.False(result.Item.Value.CanWrite);
        Assert.True(result.Item.Value.TryGetBuffer(out ArraySegment<byte> cached));
        Assert.NotSame(input.GetBuffer(), cached.Array);
        Assert.Equal(Encoding.UTF8.GetByteCount("original"), cached.Array.Length);
    }

    [Fact]
    public void Incremental_ConcurrentResultsAreStableAndUnique()
    {
        using var context = new CacheContext();
        const int operations = 1000;
        var results = new GetCacheItemResult[operations];

        Parallel.For(0, operations, i => results[i] = context.Cache.GetIncremental("counter", TimeSpan.FromMinutes(5)));

        int[] values = results.Select(x => BitConverter.ToInt32(x.Item.Value.ToArray())).OrderBy(x => x).ToArray();
        Assert.Equal(Enumerable.Range(1, operations).ToArray(), values);
        Assert.Equal(1, context.Cache.Count);
        Assert.Equal(1, ExpirationEntryCount(context.Cache));
    }

    [Fact]
    public void Incremental_ValueOnlyVersionsShareOneWarningElectionAndExactReadCount()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        HorseCacheItem original = cache.Set("counter", new MemoryStream(BitConverter.GetBytes(0)),
            TimeSpan.FromMinutes(5), expirationWarning: TimeSpan.FromSeconds(-1)).Item;
        const int reads = 2048;
        int firstReceivers = 0;
        int misses = 0;

        Parallel.For(0, reads * 2, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            if (i % 2 == 0)
            {
                if (!cache.TryGet("counter", out CacheReadResult result))
                    Interlocked.Increment(ref misses);
                else if (result.IsFirstWarningReceiver)
                    Interlocked.Increment(ref firstReceivers);
            }
            else
                cache.GetIncremental("counter", TimeSpan.FromMinutes(5), incrementValue: 0);
        });

        HorseCacheItem current = cache.GetIncremental("counter", TimeSpan.FromMinutes(5), incrementValue: 0).Item;
        Assert.Equal(0, misses);
        Assert.Equal(1, firstReceivers);
        Assert.Equal(reads, current.ExpirationWarnCount);
        Assert.Equal(reads, original.ExpirationWarnCount);
        Assert.Equal(0, BitConverter.ToInt32(current.Value.ToArray()));
    }

    [Fact]
    public void TagIndex_ReplacementRemovesOldMembershipAndRetainsSharedTags()
    {
        using var context = new CacheContext();
        HorseCache cache = context.Cache;
        cache.Set("replacement", "old", TimeSpan.FromMinutes(5), tags: ["old", "shared"]);
        cache.Set("untouched", "value", TimeSpan.FromMinutes(5), tags: ["old"]);
        cache.Set("replacement", "new", TimeSpan.FromMinutes(5), tags: ["new", "shared"]);

        cache.PurgeByTag("OLD");
        Assert.True(cache.TryGet("replacement", out _));
        Assert.False(cache.TryGet("untouched", out _));
        Assert.Equal(1, cache.Count);

        cache.PurgeByTag("SHARED");
        Assert.False(cache.TryGet("replacement", out _));
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, ExpirationEntryCount(cache));
    }

    private static void RunExpirationCleanup(HorseCache cache)
        => typeof(HorseCache).GetMethod("RemoveExpiredKeys", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cache, null);

    private static int ExpirationEntryCount(HorseCache cache)
    {
        var partitions = (Array)typeof(HorseCache).GetField("_partitions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cache)!;
        int count = 0;
        foreach (object partition in partitions)
        {
            object index = partition.GetType().GetField("Expirations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(partition)!;
            var heap = (ICollection)index.GetType().GetField("_heap", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(index)!;
            count += heap.Count;
        }
        return count;
    }

    private sealed class CacheContext : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "horse-core-performance-" + Guid.NewGuid().ToString("N"));
        internal HorseCache Cache { get; }

        internal CacheContext()
        {
            Cache = HorseRiderBuilder.Create().ConfigureOptions(options => options.DataPath = _directory).Build().Cache;
        }

        public void Dispose()
        {
            Cache.Dispose();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }
    }
}
