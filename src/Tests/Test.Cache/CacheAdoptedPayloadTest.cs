using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Xunit;

namespace Test.Cache;

/// <summary>
/// Values received over the protocol are stored without a second payload copy,
/// while values supplied through the public API keep their copy semantics.
/// </summary>
public class CacheAdoptedPayloadTest
{
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    private static MemoryStream WireContent(byte[] payload, int capacity)
    {
        // HorseProtocolReader creates content as new MemoryStream(length) and writes into it.
        MemoryStream content = new MemoryStream(capacity);
        content.Write(payload);
        return content;
    }

    private static byte[] Payload(int length) => Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

    private sealed class IsolatedCache : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "horse-cache-adopt-" + Guid.NewGuid().ToString("N"));
        public Horse.Messaging.Server.Cache.HorseCache Cache { get; }

        public IsolatedCache() => Cache = HorseRiderBuilder.Create().ConfigureOptions(o => o.DataPath = _path).Build().Cache;

        public async ValueTask DisposeAsync()
        {
            await Cache.DisposeAsync();
            if (Directory.Exists(_path))
                Directory.Delete(_path, true);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(100 * 1024)]
    public async Task ReceivedContent_IsWrappedReadOnlyWithoutCopy(int length)
    {
        await using var isolated = new IsolatedCache();
        var cache = isolated.Cache;
        byte[] payload = Payload(length);
        MemoryStream content = WireContent(payload, length);

        var operation = await cache.SetAsync(null, false, "key", content, Duration);

        Assert.True(operation.Item.Value.TryGetBuffer(out ArraySegment<byte> stored));
        Assert.Same(content.GetBuffer(), stored.Array);
        Assert.False(operation.Item.Value.CanWrite);
        Assert.Equal(payload, operation.Item.Value.ToArray());
    }

    [Fact]
    public async Task ReceivedContent_WithSpareCapacity_IsCopiedToExactSize()
    {
        await using var isolated = new IsolatedCache();
        var cache = isolated.Cache;
        byte[] payload = Payload(100);
        MemoryStream content = WireContent(payload, 4096);

        var operation = await cache.SetAsync(null, false, "key", content, Duration);

        Assert.True(operation.Item.Value.TryGetBuffer(out ArraySegment<byte> stored));
        Assert.NotSame(content.GetBuffer(), stored.Array);
        Assert.Equal(payload.Length, stored.Array.Length);
        Assert.Equal(payload, operation.Item.Value.ToArray());
    }

    [Fact]
    public async Task ReceivedContent_WithHiddenBuffer_IsCopied()
    {
        await using var isolated = new IsolatedCache();
        var cache = isolated.Cache;
        byte[] payload = Payload(64);

        var operation = await cache.SetAsync(null, false, "key", new MemoryStream(payload), Duration);

        payload[0] = 255;
        Assert.Equal(0, operation.Item.Value.ToArray()[0]);
        Assert.False(operation.Item.Value.CanWrite);
    }

    [Fact]
    public async Task ReceivedEmptyContent_IsStoredAsEmptyValue()
    {
        await using var isolated = new IsolatedCache();
        var cache = isolated.Cache;

        var operation = await cache.SetAsync(null, false, "key", null, Duration);

        Assert.Equal(0, operation.Item.Value.Length);
        Assert.False(operation.Item.Value.CanWrite);
    }

    [Fact]
    public async Task PublicSet_CopiesCallerStream()
    {
        await using var isolated = new IsolatedCache();
        var cache = isolated.Cache;
        byte[] payload = Payload(64);
        MemoryStream callerStream = WireContent(payload, payload.Length);

        var operation = await cache.SetAsync("key", callerStream, Duration);

        Assert.True(operation.Item.Value.TryGetBuffer(out ArraySegment<byte> stored));
        Assert.NotSame(callerStream.GetBuffer(), stored.Array);
        callerStream.Position = 0;
        callerStream.WriteByte(255);
        Assert.Equal(payload, operation.Item.Value.ToArray());
    }

    [Fact]
    public async Task ClientSet_LargeValue_StoredReadOnlyAndReturnedIntact()
    {
        await using var ctx = await CacheTestServer.Create();
        HorseClient client = new HorseClient();
        await client.ConnectAsync($"horse://localhost:{ctx.Port}");
        try
        {
            byte[] first = Payload(100 * 1024);
            byte[] second = Enumerable.Repeat((byte)7, 100 * 1024).ToArray();

            Assert.Equal(HorseResultCode.Ok, (await client.Cache.SetData("big", first, CancellationToken.None)).Code);
            Assert.True(ctx.Rider.Cache.TryGet("big", out var stored));
            Assert.False(stored.Item.Value.CanWrite);
            Assert.True(stored.Item.Value.TryGetBuffer(out ArraySegment<byte> buffer));
            Assert.Equal(first.Length, buffer.Array.Length);

            Assert.Equal(HorseResultCode.Ok, (await client.Cache.SetData("big", second, CancellationToken.None)).Code);

            // Replacing the entry must not disturb the earlier value's buffer.
            Assert.Equal(first, stored.Item.Value.ToArray());
            var read = await client.Cache.GetData("big", CancellationToken.None);
            Assert.Equal(second, read.Value);
        }
        finally
        {
            client.Disconnect();
        }
    }
}
