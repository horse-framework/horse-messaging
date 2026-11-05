using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Cache;

/// <summary>
/// Orders persistent cache mutations and batches asynchronous writers.
/// A successful write task means that the value (or a newer value in its batch)
/// has been written to the destination file. Atomic replacement is optional;
/// neither mode requests a physical disk flush.
/// </summary>
internal sealed class CachePersistenceWriter : IAsyncDisposable
{
    private const int QueueCapacity = 1024;
    private const int MaximumBatchSize = 64;

    private readonly string _directory;
    private readonly bool _atomicWrites;
    private readonly Channel<Request> _requests = Channel.CreateBounded<Request>(new BoundedChannelOptions(QueueCapacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });

    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _admissionGate = new();
    private Task _admissionTail = Task.CompletedTask;
    private int _processing;
    private int _workers;
    private int _disposed;
    private bool _directoryReady;

    internal CachePersistenceWriter(string directory) : this(directory, true)
    {
    }

    internal CachePersistenceWriter(string directory, bool atomicWrites)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = Path.GetFullPath(directory);
        _atomicWrites = atomicWrites;
    }

    internal Task WriteAsync(HorseCacheItem item)
        => Enqueue(CreateWrite(item));

    /// <summary>
    /// Avoids a ThreadPool round trip for synchronous callers when the writer
    /// is idle. Concurrent mutations still enter the ordered bounded queue.
    /// </summary>
    internal Task Write(HorseCacheItem item)
    {
        Request request = CreateWrite(item);
        lock (_admissionGate)
        {
            if (_disposed != 0)
                return Task.FromException(new ObjectDisposedException(nameof(CachePersistenceWriter)));

            if (!_admissionTail.IsCompleted || _requests.Reader.TryPeek(out _) || Interlocked.CompareExchange(ref _processing, 1, 0) != 0)
                return Enqueue(request);

            Interlocked.Increment(ref _workers);
        }

        try
        {
            ProcessSingleWrite(request);
        }
        finally
        {
            FinishWorker();
        }
        return request.Completion.Task;
    }

    private static Request CreateWrite(HorseCacheItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string key = item.CacheKey ?? item.Key;
        ValidateKey(key);
        return new Request(RequestKind.Write, key, new PersistentValue(item));
    }

    internal Task DeleteAsync(string key)
    {
        ValidateKey(key);
        return Enqueue(new Request(RequestKind.Delete, key));
    }

    internal Task PurgeAsync() => Enqueue(new Request(RequestKind.Purge));

    internal Task FlushAsync() => Enqueue(new Request(RequestKind.Flush));

    internal static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (key is "." or ".." || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.IndexOfAny(['/', '\\']) >= 0)
            throw new ArgumentException("A persistent cache key must be a file name without directory separators.", nameof(key));
    }

    private Task Enqueue(Request request)
    {
        lock (_admissionGate)
        {
            if (_disposed != 0)
                return Task.FromException(new ObjectDisposedException(nameof(CachePersistenceWriter)));

            if (_admissionTail.IsCompleted && _requests.Writer.TryWrite(request))
            {
                StartWorker();
                return request.Completion.Task;
            }

            // Once a caller is backpressured, serialize subsequent admissions
            // explicitly. A newer TryWrite must not pass an older mutation.
            TaskCompletionSource admitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task result = EnqueueSlowAsync(request, _admissionTail, admitted);
            _admissionTail = admitted.Task;
            return result;
        }
    }

    private async Task EnqueueSlowAsync(Request request, Task previousAdmission, TaskCompletionSource admitted)
    {
        try
        {
            await previousAdmission.ConfigureAwait(false);
            StartWorker();
            await _requests.Writer.WriteAsync(request).ConfigureAwait(false);
            StartWorker();
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(nameof(CachePersistenceWriter));
        }
        finally
        {
            admitted.TrySetResult();
        }

        await request.Completion.Task.ConfigureAwait(false);
    }

    private void StartWorker()
    {
        if (Interlocked.CompareExchange(ref _processing, 1, 0) == 0)
        {
            Interlocked.Increment(ref _workers);
            _ = Task.Run(ProcessQueue);
        }
    }

    private void ProcessQueue()
    {
        Request pending = null;
        try
        {
            while (pending != null || _requests.Reader.TryRead(out pending))
            {
                Request current = pending;
                pending = null;
                if (current.Kind != RequestKind.Write)
                {
                    ProcessBarrier(current);
                    continue;
                }

                if (!_requests.Reader.TryRead(out Request next))
                {
                    ProcessSingleWrite(current);
                    continue;
                }

                if (next.Kind != RequestKind.Write)
                {
                    pending = next;
                    ProcessSingleWrite(current);
                    continue;
                }

                List<Request> batch = new(4) { current, next };
                while (batch.Count < MaximumBatchSize && _requests.Reader.TryRead(out next))
                {
                    if (next.Kind != RequestKind.Write)
                    {
                        pending = next;
                        break;
                    }

                    batch.Add(next);
                }

                ProcessWrites(batch);
            }
        }
        finally
        {
            FinishWorker();
        }
    }

    private void FinishWorker()
    {
        Volatile.Write(ref _processing, 0);
        if (_requests.Reader.TryPeek(out _))
            StartWorker();

        // A new drain can start after _processing is released. Count the
        // retiring drain too so disposal never races an in-flight write.
        if (Interlocked.Decrement(ref _workers) == 0 && Volatile.Read(ref _disposed) != 0 && _requests.Reader.Completion.IsCompleted)
            _stopped.TrySetResult();
    }

    private void ProcessSingleWrite(Request request)
    {
        try
        {
            WriteFile(request.Key, request.Value);
            request.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            request.Completion.TrySetException(exception);
        }
    }

    private void ProcessWrites(List<Request> batch)
    {
        Dictionary<string, Request> latest = new(StringComparer.OrdinalIgnoreCase);
        foreach (Request request in batch)
            latest[request.Key] = request;

        Dictionary<string, Exception> failures = null;
        foreach (KeyValuePair<string, Request> pair in latest)
        {
            try
            {
                WriteFile(pair.Value.Key, pair.Value.Value);
            }
            catch (Exception exception)
            {
                failures ??= new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
                failures.Add(pair.Key, exception);
            }
        }

        foreach (Request request in batch)
        {
            if (failures != null && failures.TryGetValue(request.Key, out Exception exception))
                request.Completion.TrySetException(exception);
            else
                request.Completion.TrySetResult();
        }
    }

    private void ProcessBarrier(Request request)
    {
        try
        {
            if (request.Kind == RequestKind.Delete)
                File.Delete(GetFilename(request.Key));
            else if (request.Kind == RequestKind.Purge && Directory.Exists(_directory))
            {
                foreach (string filename in Directory.EnumerateFiles(_directory, "*.hci"))
                    File.Delete(filename);
                foreach (string filename in Directory.EnumerateFiles(_directory, ".cache-*.tmp"))
                    File.Delete(filename);
            }

            request.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            request.Completion.TrySetException(exception);
        }
    }

    private string GetFilename(string key) => Path.Combine(_directory, key + ".hci");

    private void WriteFile(string key, PersistentValue value)
    {
        if (!_directoryReady)
        {
            Directory.CreateDirectory(_directory);
            _directoryReady = true;
        }

        string temporary = _atomicWrites ? Path.Combine(_directory, ".cache-" + Guid.NewGuid().ToString("N") + ".tmp") : null;
        bool committed = false;
        try
        {
            string destination = GetFilename(key);
            using (FileStream stream = new(temporary ?? destination, _atomicWrites ? FileMode.CreateNew : FileMode.Create,
                       FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
                WriteContents(stream, key, value);

            if (_atomicWrites)
                File.Move(temporary, destination, true);
            committed = true;
        }
        catch (DirectoryNotFoundException)
        {
            _directoryReady = false;
            throw;
        }
        finally
        {
            if (_atomicWrites && !committed && File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void WriteContents(FileStream stream, string key, PersistentValue value)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(key);
        writer.Write(value.Expiration);
        writer.Write(value.WarnCount);
        writer.Write(value.Warning.HasValue);
        if (value.Warning.HasValue)
            writer.Write(value.Warning.Value);

        writer.Write(value.Tags.Length);
        foreach (string tag in value.Tags)
            writer.Write(tag);

        writer.Write(value.Length);
        if (value.Buffer.HasValue)
        {
            ArraySegment<byte> buffer = value.Buffer.Value;
            writer.Write(buffer.Array, buffer.Offset, buffer.Count);
        }
        else
            value.Stream.WriteTo(stream);

        writer.Flush();
    }

    public ValueTask DisposeAsync()
    {
        lock (_admissionGate)
        {
            if (_disposed == 0)
            {
                Volatile.Write(ref _disposed, 1);
                _ = CompleteAfterAdmissionsAsync(_admissionTail);
            }
        }

        return new ValueTask(_stopped.Task);
    }

    private async Task CompleteAfterAdmissionsAsync(Task admitted)
    {
        await admitted.ConfigureAwait(false);
        _requests.Writer.TryComplete();
        StartWorker();
    }

    private enum RequestKind
    {
        Write,
        Delete,
        Purge,
        Flush
    }

    private sealed class Request
    {
        internal readonly RequestKind Kind;
        internal readonly string Key;
        internal readonly PersistentValue Value;
        internal readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Request(RequestKind kind, string key = null, PersistentValue value = null)
        {
            Kind = kind;
            Key = key;
            Value = value;
        }
    }

    private sealed class PersistentValue
    {
        internal readonly long Expiration;
        internal readonly long? Warning;
        internal readonly int WarnCount;
        internal readonly string[] Tags;
        internal readonly MemoryStream Stream;
        internal readonly ArraySegment<byte>? Buffer;
        internal readonly int Length;

        internal PersistentValue(HorseCacheItem item)
        {
            ArgumentNullException.ThrowIfNull(item.Value);
            Expiration = item.Expiration.ToUnixMilliseconds();
            Warning = item.ExpirationWarning?.ToUnixMilliseconds();
            WarnCount = item.ExpirationWarnCount;
            Tags = item.TagValues.Length == 0 ? Array.Empty<string>() : (string[])item.TagValues.Clone();
            Stream = item.Value;
            Length = checked((int)Stream.Length);
            if (Stream.TryGetBuffer(out ArraySegment<byte> buffer))
                Buffer = buffer;
        }
    }
}
