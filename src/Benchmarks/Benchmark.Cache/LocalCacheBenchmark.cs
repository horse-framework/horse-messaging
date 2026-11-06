using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using CacheManager = Horse.Messaging.Server.Cache.HorseCache;

namespace Benchmark.Cache;

/// <summary>
/// Repeatable cache microbenchmarks without a TCP server. Run in Release:
/// dotnet run --project src/Benchmarks/Benchmark.Cache -c Release -- --local
/// </summary>
internal static class LocalCacheBenchmark
{
    private static readonly TimeSpan Duration = TimeSpan.FromHours(1);
    private static object _sink;

    internal static async Task Run()
    {
        Console.WriteLine("Local cache microbenchmarks; three samples, median time and allocation; no network latency.");
        Console.WriteLine($".NET {Environment.Version}; {RuntimeInformation.ProcessArchitecture}; CPUs={Environment.ProcessorCount}; ServerGC={GCSettings.IsServerGC}");
        Console.WriteLine("Synchronous allocation uses the current thread; parallel/async allocation uses the whole process.");
        Console.WriteLine("Persistence uses the reported AtomicPersistentWrites mode; completion does not request a physical disk flush.");
#if DEBUG
        Console.WriteLine("DEBUG BUILD: use -c Release for performance comparisons.");
#endif
        string directory = Path.Combine(Path.GetTempPath(), "horse-cache-benchmark-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using CacheManager cache = CreateCache(directory);
            Console.WriteLine($"AtomicPersistentWrites={cache.Options.AtomicPersistentWrites} (default-mode measurements)");
            cache.Set("hot", "value", Duration);
            Measure("TryGet hit", 1_000_000, () =>
            {
                if (!cache.TryGet("hot", out var result))
                    throw new InvalidOperationException("The hot benchmark entry is missing.");
                _sink = result.Item;
            });
            Measure("Get compatibility hit", 250_000, () => _sink = cache.Get("hot").GetAwaiter().GetResult());

            MeasureOwnedPayload(cache);
            MeasureProtocol();

            Seed(cache, 100_000);
            cache.Purge();
            cache.Options.MaximumKeys = 1_000_000;
            MeasureParallelSet(cache);
            cache.Purge();

            Seed(cache, 100_000);
            Measure("GetCacheKeys full 100k", 5, () => _sink = cache.GetCacheKeys(), 1);
            Measure("GetCacheKeys prefix page20 /100k", 20, () => _sink = cache.GetCacheKeys("seed-09*", 0, 20), 2);

            MethodInfo cleanupMethod = typeof(CacheManager).GetMethod("RemoveExpiredKeys", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(CacheManager).FullName, "RemoveExpiredKeys");
            Action cleanup = cleanupMethod.CreateDelegate<Action>(cache);
            Measure("Expiry cleanup 100k unexpired", 1000, cleanup, 20);

            MeasurePrepared("PurgeByTag 10 matches /100k", 100,
                () =>
                {
                    for (int i = 0; i < 10; i++)
                        cache.Set("rare-" + i, "value", Duration, tags: ["rare"]);
                },
                () => cache.PurgeByTag("rare"));

            cache.Purge();
            await MeasurePersistence(cache);
            await MeasureAtomicPersistence(Path.Combine(directory, "atomic-mode"));
            await cache.FlushAsync();
            GC.KeepAlive(_sink);
        }
        finally
        {
            _sink = null;
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static CacheManager CreateCache(string directory, bool atomicWrites = false)
    {
        CacheManager cache = HorseRiderBuilder.Create()
            .ConfigureOptions(options => options.DataPath = directory)
            .ConfigureCache(configure => configure.Options.AtomicPersistentWrites = atomicWrites)
            .Build().Cache;
        cache.Options.ExpirationWarningIsEnabled = false;
        return cache;
    }

    private static void Seed(CacheManager cache, int count)
    {
        for (int i = 0; i < count; i++)
            cache.Set("seed-" + i.ToString("D6", CultureInfo.InvariantCulture), "value", Duration);
        Console.WriteLine($"Prepared {count:N0} unexpired entries; total live count={cache.Count:N0}.");
    }

    private static void MeasureOwnedPayload(CacheManager cache)
    {
        using MemoryStream input = new(new byte[1024 * 1024]);
        cache.Set("owned", input, Duration);
        if (!cache.TryGet("owned", out var result) || !result.Item.Value.TryGetBuffer(out _))
            throw new InvalidOperationException("The cache-owned payload must expose its buffer to the persistence writer.");
        MemoryStream payload = result.Item.Value;
        Measure("Owned 1MiB TryGetBuffer", 100_000, () =>
        {
            payload.TryGetBuffer(out ArraySegment<byte> buffer);
            _sink = buffer.Array;
        });
        Measure("Explicit 1MiB ToArray copy baseline", 100, () => _sink = payload.ToArray(), 2);
    }

    private static void MeasureProtocol()
    {
        foreach (int size in new[] { 64 * 1024, 1024 * 1024 })
        {
            byte[] bytes = new byte[size];
            using MemoryStream payload = new(bytes, 0, bytes.Length, false, true);
            HorseMessage message = new(MessageType.Response, "payload", 0) { Content = payload };
            Measure($"Protocol.Create {size / 1024}KiB", size < 1024 * 1024 ? 2000 : 300,
                () => _sink = HorseProtocolWriter.Create(message), 20);
        }
    }

    private static void MeasureParallelSet(CacheManager cache)
    {
        const int workers = 8;
        const int iterations = 10_000;
        void Write(int count) => Parallel.For(0, workers, worker =>
        {
            string key = "worker-" + worker;
            using MemoryStream payload = new(new byte[16]);
            for (int i = 0; i < count; i++)
                cache.Set(key, payload, Duration);
        });
        Write(1000);
        Sample[] samples = new Sample[3];
        for (int i = 0; i < samples.Length; i++)
        {
            Collect();
            long allocated = GC.GetTotalAllocatedBytes(true);
            long started = Stopwatch.GetTimestamp();
            Write(iterations);
            double nanoseconds = Stopwatch.GetElapsedTime(started).TotalNanoseconds;
            samples[i] = new Sample(nanoseconds / (workers * iterations), (double)(GC.GetTotalAllocatedBytes(true) - allocated) / (workers * iterations));
        }
        Report("Set overwrite 8 workers /limit on /after100k", samples);
    }

    private static async Task MeasurePersistence(CacheManager cache)
    {
        const int sequential = 100;
        const int burst = 1000;
        string payload = new('p', 1024);
        for (int i = 0; i < 5; i++)
            await cache.SetAsync("persistent", payload, Duration, persistent: true);

        await MeasureAsync("Persistent Set 1KiB synchronous", sequential, () =>
        {
            for (int i = 0; i < sequential; i++)
                cache.Set("persistent", payload, Duration, persistent: true);
            cache.FlushAsync().GetAwaiter().GetResult();
            return Task.CompletedTask;
        });
        await MeasureAsync("Persistent SetAsync 1KiB sequential", sequential, async () =>
        {
            for (int i = 0; i < sequential; i++)
                await cache.SetAsync("persistent", payload, Duration, persistent: true);
            await cache.FlushAsync();
        });
        await MeasureAsync("Persistent SetAsync 1KiB same-key burst", burst, async () =>
        {
            Task[] writes = new Task[burst];
            for (int i = 0; i < writes.Length; i++)
                writes[i] = cache.SetAsync("persistent", payload, Duration, persistent: true).AsTask();
            await Task.WhenAll(writes);
            await cache.FlushAsync();
        });
    }

    private static async Task MeasureAtomicPersistence(string directory)
    {
        await using CacheManager cache = CreateCache(directory, true);
        const int count = 100;
        string payload = new('p', 1024);
        for (int i = 0; i < 5; i++)
            cache.Set("persistent", payload, Duration, persistent: true);

        await MeasureAsync("Persistent Set 1KiB atomic sync (opt-in)", count, () =>
        {
            for (int i = 0; i < count; i++)
                cache.Set("persistent", payload, Duration, persistent: true);
            cache.FlushAsync().GetAwaiter().GetResult();
            return Task.CompletedTask;
        });
    }

    private static void Measure(string label, int count, Action action, int warmup = 2000)
    {
        for (int i = 0; i < Math.Min(count, warmup); i++)
            action();
        Sample[] samples = new Sample[3];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            Collect();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++)
                action();
            double nanoseconds = Stopwatch.GetElapsedTime(started).TotalNanoseconds;
            samples[sample] = new Sample(nanoseconds / count, (double)(GC.GetAllocatedBytesForCurrentThread() - allocated) / count);
        }
        Report(label, samples);
    }

    private static void MeasurePrepared(string label, int count, Action prepare, Action action)
    {
        for (int i = 0; i < 10; i++)
        {
            prepare();
            action();
        }
        Sample[] samples = new Sample[3];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            Collect();
            double nanoseconds = 0;
            long allocated = 0;
            for (int i = 0; i < count; i++)
            {
                prepare();
                long before = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                action();
                nanoseconds += Stopwatch.GetElapsedTime(started).TotalNanoseconds;
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            samples[sample] = new Sample(nanoseconds / count, (double)allocated / count);
        }
        Report(label + " (setup excluded)", samples);
    }

    private static async Task MeasureAsync(string label, int count, Func<Task> action)
    {
        await action();
        Sample[] samples = new Sample[3];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            Collect();
            long allocated = GC.GetTotalAllocatedBytes(true);
            long started = Stopwatch.GetTimestamp();
            await action();
            double nanoseconds = Stopwatch.GetElapsedTime(started).TotalNanoseconds;
            samples[sample] = new Sample(nanoseconds / count, (double)(GC.GetTotalAllocatedBytes(true) - allocated) / count);
        }
        Report(label, samples);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void Report(string label, Sample[] samples)
    {
        Array.Sort(samples, (a, b) => a.Nanoseconds.CompareTo(b.Nanoseconds));
        double nanoseconds = samples[1].Nanoseconds;
        double[] allocations = [samples[0].Bytes, samples[1].Bytes, samples[2].Bytes];
        Array.Sort(allocations);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0}: {1:F1} ns/op; {2:F1} B/op; {3:F0} ops/s", label, nanoseconds, allocations[1], 1_000_000_000 / nanoseconds));
    }

    private readonly record struct Sample(double Nanoseconds, double Bytes);
}
