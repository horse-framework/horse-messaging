using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Server.Cache;
using Xunit;

namespace Test.Cache;

public class CachePersistenceWriterTest
{
    [Fact]
    public async Task DirectFileMode_PreservesFormatAndOrdersQueuedMutations()
    {
        await using WriterScope scope = new(false);
        HorseCacheItem first = Item("same", "first");
        first.Tags = ["tag"];
        first.ExpirationWarning = first.Expiration.AddMinutes(-1);
        first.ExpirationWarnCount = 3;
        await scope.Write(first);
        StoredValue initial = Read(scope.Filename("same"));
        Assert.Equal("first", initial.Payload);
        Assert.Equal(["tag"], initial.Tags);
        Assert.Equal(3, initial.WarnCount);
        Assert.NotNull(initial.Warning);

        using BlockingStream gate = new();
        Task older = scope.Write(Item("same", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task middle = scope.Write(Item("same", "middle"));
        Task delete = scope.Delete("same");
        Task last = scope.Write(Item("same", "last"));
        gate.Release.Set();
        await Task.WhenAll(older, middle, delete, last).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("last", Read(scope.Filename("same")).Payload);
        Assert.Equal(["same.hci"], Directory.GetFiles(scope.DirectoryPath).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public async Task InFlightWrite_HoldsQueuedBarriersInSubmissionOrder()
    {
        await using WriterScope scope = new();
        using BlockingStream gate = new();
        Task inFlight = scope.Write(Item("same", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Task[] operations = await Task.Run(() => new[]
        {
            scope.Write(Item("same", "queued")),
            scope.Delete("same"),
            scope.Write(Item("before-purge", "queued")),
            scope.Purge(),
            scope.Write(Item("after-purge", "last"))
        }).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(operations, operation => Assert.False(operation.IsCompleted));

        gate.Release.Set();
        await inFlight.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["after-purge.hci"], Directory.GetFiles(scope.DirectoryPath).Select(Path.GetFileName).ToArray());
        Assert.Equal("last", Read(scope.Filename("after-purge")).Payload);
    }

    [Fact]
    public async Task QueuedWrites_CoalesceAndApplyBackpressureWithoutLosingLatestValue()
    {
        await using WriterScope scope = new();
        using BlockingStream gate = new();
        Task blocked = scope.Write(Item("gate", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        const int count = 1500; // Exceeds both the batch size and the bounded queue.
        int payloadWrites = 0;
        List<Task> writes = new(count);
        for (int i = 0; i < count; i++)
            writes.Add(scope.Write(Item("latest", new CountingStream(i.ToString(), () => Interlocked.Increment(ref payloadWrites)))));

        Task flush = scope.Flush();
        Assert.False(flush.IsCompleted);
        gate.Release.Set();
        await blocked.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.WhenAll(writes).WaitAsync(TimeSpan.FromSeconds(10));
        await flush.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal((count - 1).ToString(), Read(scope.Filename("latest")).Payload);
        Assert.InRange(payloadWrites, 1, count - 1);
        Assert.Empty(Directory.GetFiles(scope.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public async Task DeleteAndPurge_AreOrderingBarriersBetweenWriteBatches()
    {
        await using WriterScope scope = new();
        using BlockingStream gate = new();
        Task blocked = scope.Write(Item("gate", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Task[] operations =
        [
            scope.Write(Item("before-delete", "old")),
            scope.Delete("before-delete"),
            scope.Write(Item("before-purge", "old")),
            scope.Purge(),
            scope.Write(Item("after-purge", "new")),
            scope.Flush()
        ];
        gate.Release.Set();
        await blocked.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["after-purge.hci"], Directory.GetFiles(scope.DirectoryPath).Select(Path.GetFileName).ToArray());
        Assert.Equal("new", Read(scope.Filename("after-purge")).Payload);
    }

    [Fact]
    public async Task BackpressuredBarriers_KeepSubmissionOrderAndDisposeDrainsPendingAdmissions()
    {
        await using WriterScope scope = new();
        using BlockingStream gate = new();
        Task blocked = scope.Write(Item("gate", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        List<Task> operations = new();
        for (int i = 0; i < 1200; i++)
            operations.Add(scope.Write(Item("same", i.ToString())));
        operations.Add(scope.Delete("same"));
        operations.Add(scope.Write(Item("after-delete", "later")));
        operations.Add(scope.Purge());
        operations.Add(scope.Write(Item("after-purge", "last")));
        Task dispose = scope.DisposeWriterAsync();

        gate.Release.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(10));
        await blocked;
        await Task.WhenAll(operations);

        Assert.Equal(["after-purge.hci"], Directory.GetFiles(scope.DirectoryPath).Select(Path.GetFileName).ToArray());
        Assert.Equal("last", Read(scope.Filename("after-purge")).Payload);
    }

    [Fact]
    public async Task Write_CapturesMetadataAndWritesExactBufferSegmentInExistingFormat()
    {
        await using WriterScope scope = new();
        using BlockingStream gate = new();
        Task blocked = scope.Write(Item("gate", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        DateTime expiration = DateTime.UtcNow.AddMinutes(5);
        DateTime warning = expiration.AddMinutes(-1);
        HorseCacheItem item = Item("snapshot", new MemoryStream(Encoding.UTF8.GetBytes("__abc__"), 2, 3, false, true));
        item.Expiration = expiration;
        item.ExpirationWarning = warning;
        item.ExpirationWarnCount = 7;
        item.Tags = ["one", "two"];
        Task write = scope.Write(item);

        item.Key = "changed";
        item.Tags = ["changed"];
        item.Expiration = expiration.AddDays(1);
        item.ExpirationWarning = null;
        item.ExpirationWarnCount = 99;
        item.Value = new MemoryStream(Encoding.UTF8.GetBytes("changed"));
        gate.Release.Set();
        await blocked.WaitAsync(TimeSpan.FromSeconds(10));
        await write.WaitAsync(TimeSpan.FromSeconds(10));

        StoredValue stored = Read(scope.Filename("snapshot"));
        Assert.Equal("snapshot", stored.Key);
        Assert.Equal(new DateTimeOffset(expiration).ToUnixTimeMilliseconds(), stored.Expiration);
        Assert.Equal(new DateTimeOffset(warning).ToUnixTimeMilliseconds(), stored.Warning);
        Assert.Equal(7, stored.WarnCount);
        Assert.Equal(["one", "two"], stored.Tags);
        Assert.Equal("abc", stored.Payload);
    }

    [Fact]
    public async Task FailedBatch_FaultsEveryWaiterAndWorkerCanRecover()
    {
        await using WriterScope scope = new();
        File.WriteAllText(scope.DirectoryPath, "directory path is occupied by a file");
        Task[] writes = Enumerable.Range(0, 40).Select(i => scope.Write(Item("same", i.ToString()))).ToArray();

        foreach (Task write in writes)
            await Assert.ThrowsAnyAsync<IOException>(() => write);

        File.Delete(scope.DirectoryPath);
        await scope.Write(Item("same", "recovered"));
        await scope.Flush();
        Assert.Equal("recovered", Read(scope.Filename("same")).Payload);
    }

    [Fact]
    public async Task FailedReplacement_PreservesPreviousFileAndRemovesTemporaryFile()
    {
        await using WriterScope scope = new();
        await scope.Write(Item("same", "original"));
        await Assert.ThrowsAsync<IOException>(() => scope.Write(Item("same", new ThrowingStream())));

        Assert.Equal("original", Read(scope.Filename("same")).Payload);
        Assert.Empty(Directory.GetFiles(scope.DirectoryPath, "*.tmp"));
        await scope.Write(Item("same", "recovered"));
        Assert.Equal("recovered", Read(scope.Filename("same")).Payload);
    }

    [Fact]
    public async Task Dispose_DrainsAcceptedWritesAndRejectsFurtherOperations()
    {
        await using WriterScope scope = new();
        using BlockingStream gate = new();
        Task blocked = scope.Write(Item("gate", gate));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task queued = scope.Write(Item("queued", "saved"));
        Task dispose = scope.DisposeWriterAsync();
        Assert.False(dispose.IsCompleted);

        gate.Release.Set();
        await dispose.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.WhenAll(blocked, queued);

        Assert.Equal("saved", Read(scope.Filename("queued")).Payload);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => scope.Write(Item("rejected", "no")));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => scope.Flush());
    }

    [Fact]
    public async Task Dispose_AfterIdleDrainRestart_WaitsForAcceptedWrite()
    {
        for (int i = 0; i < 20; i++)
        {
            await using WriterScope scope = new();
            await scope.Write(Item("first", "first"));
            Task write = scope.Write(Item("last", "last"));
            await scope.DisposeWriterAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(write.IsCompletedSuccessfully);
            Assert.Equal("last", Read(scope.Filename("last")).Payload);
        }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("/absolute")]
    public async Task PersistentKeys_CannotEscapeDirectory(string key)
    {
        await using WriterScope scope = new();
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Write(Item(key, "payload")));
        Assert.False(Directory.Exists(scope.DirectoryPath));
    }

    private static HorseCacheItem Item(string key, string value) => Item(key, new MemoryStream(Encoding.UTF8.GetBytes(value)));

    private static HorseCacheItem Item(string key, MemoryStream value) => new()
    {
        Key = key,
        Expiration = DateTime.UtcNow.AddMinutes(5),
        IsPersistent = true,
        Tags = [],
        Value = value
    };

    private static StoredValue Read(string filename)
    {
        using BinaryReader reader = new(File.OpenRead(filename), Encoding.UTF8);
        string key = reader.ReadString();
        long expiration = reader.ReadInt64();
        int warnCount = reader.ReadInt32();
        long? warning = reader.ReadBoolean() ? reader.ReadInt64() : null;
        string[] tags = new string[reader.ReadInt32()];
        for (int i = 0; i < tags.Length; i++)
            tags[i] = reader.ReadString();
        string payload = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
        return new StoredValue(key, expiration, warning, warnCount, tags, payload);
    }

    private sealed record StoredValue(string Key, long Expiration, long? Warning, int WarnCount, string[] Tags, string Payload);

    private sealed class BlockingStream : MemoryStream
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new();

        internal BlockingStream() : base(Encoding.UTF8.GetBytes("gate")) { }

        public override void WriteTo(Stream stream)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The persistence test did not release its write gate.");
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

    private sealed class CountingStream(string value, Action written) : MemoryStream(Encoding.UTF8.GetBytes(value))
    {
        public override void WriteTo(Stream stream)
        {
            written();
            base.WriteTo(stream);
        }
    }

    private sealed class ThrowingStream() : MemoryStream([1])
    {
        public override void WriteTo(Stream stream) => throw new IOException("Simulated payload write failure.");
    }

    private sealed class WriterScope : IAsyncDisposable
    {
        private static readonly Type WriterType = typeof(HorseCacheItem).Assembly.GetType("Horse.Messaging.Server.Cache.CachePersistenceWriter", true);
        private readonly object _writer;
        private readonly string _root = Path.Combine(Path.GetTempPath(), "horse-persistence-writer-" + Guid.NewGuid().ToString("N"));
        internal string DirectoryPath { get; }

        internal WriterScope(bool? atomicWrites = null)
        {
            Directory.CreateDirectory(_root);
            DirectoryPath = Path.Combine(_root, "Cache");
            object[] arguments = atomicWrites.HasValue ? [DirectoryPath, atomicWrites.Value] : [DirectoryPath];
            _writer = Activator.CreateInstance(WriterType, BindingFlags.Instance | BindingFlags.NonPublic, null, arguments, null);
        }

        internal string Filename(string key) => Path.Combine(DirectoryPath, key + ".hci");
        internal Task Write(HorseCacheItem item) => Invoke("WriteAsync", item);
        internal Task Delete(string key) => Invoke("DeleteAsync", key);
        internal Task Purge() => Invoke("PurgeAsync");
        internal Task Flush() => Invoke("FlushAsync");
        internal Task DisposeWriterAsync() => ((IAsyncDisposable)_writer).DisposeAsync().AsTask();

        private Task Invoke(string name, params object[] arguments)
        {
            try
            {
                return (Task)WriterType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_writer, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await ((IAsyncDisposable)_writer).DisposeAsync();
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
    }
}
