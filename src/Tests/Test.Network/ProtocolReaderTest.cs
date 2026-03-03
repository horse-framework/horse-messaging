using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests for HorseProtocolReader edge cases: partial reads, corrupted data, buffer reuse.
/// </summary>
public class ProtocolReaderTest
{
    #region Null / Empty Streams

    [Fact]
    public async Task EmptyStream_ReturnsNull()
    {
        HorseProtocolReader reader = new HorseProtocolReader();
        using MemoryStream ms = new MemoryStream();

        HorseMessage result = await reader.Read(ms);

        Assert.Null(result);
    }

    [Fact]
    public async Task StreamWithLessThan8Bytes_ReturnsNull()
    {
        HorseProtocolReader reader = new HorseProtocolReader();
        using MemoryStream ms = new MemoryStream([0x01, 0x02, 0x03]);

        HorseMessage result = await reader.Read(ms);

        Assert.Null(result);
    }

    #endregion

    #region Reusability

    [Fact]
    public async Task ReaderCanReadMultipleMessages()
    {
        HorseProtocolReader reader = new HorseProtocolReader();

        for (int i = 0; i < 10; i++)
        {
            HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
            msg.SetMessageId($"reuse-{i:D3}");
            msg.SetStringContent($"Message {i}");

            byte[] data = HorseProtocolWriter.Create(msg);
            using MemoryStream ms = new MemoryStream(data);

            HorseMessage read = await reader.Read(ms);

            Assert.NotNull(read);
            Assert.Equal($"reuse-{i:D3}", read.MessageId);
            Assert.Equal($"Message {i}", read.ToString());
        }
    }

    [Fact]
    public async Task ReaderHandlesAlternatingMessageSizes()
    {
        HorseProtocolReader reader = new HorseProtocolReader();

        // Small message
        HorseMessage small = new HorseMessage(MessageType.DirectMessage, "t");
        small.SetMessageId("s");
        small.SetStringContent("x");

        byte[] smallData = HorseProtocolWriter.Create(small);
        using MemoryStream ms1 = new MemoryStream(smallData);
        HorseMessage readSmall = await reader.Read(ms1);
        Assert.NotNull(readSmall);
        Assert.Equal("x", readSmall.ToString());

        // Large message
        string largeContent = new string('Z', 10000);
        HorseMessage large = new HorseMessage(MessageType.QueueMessage, "queue");
        large.SetMessageId("l");
        large.SetStringContent(largeContent);

        byte[] largeData = HorseProtocolWriter.Create(large);
        using MemoryStream ms2 = new MemoryStream(largeData);
        HorseMessage readLarge = await reader.Read(ms2);
        Assert.NotNull(readLarge);
        Assert.Equal(largeContent, readLarge.ToString());

        // Small again
        using MemoryStream ms3 = new MemoryStream(smallData);
        HorseMessage readSmall2 = await reader.Read(ms3);
        Assert.NotNull(readSmall2);
        Assert.Equal("x", readSmall2.ToString());
    }

    #endregion

    #region Truncated Content

    [Fact]
    public async Task TruncatedContent_ReturnsNull()
    {
        // Create a valid message then truncate the data
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("trunc");
        msg.SetStringContent("This content will be truncated");

        byte[] fullData = HorseProtocolWriter.Create(msg);

        // Remove last 10 bytes (content area)
        byte[] truncated = new byte[fullData.Length - 10];
        Array.Copy(fullData, truncated, truncated.Length);

        HorseProtocolReader reader = new HorseProtocolReader();
        using MemoryStream ms = new MemoryStream(truncated);

        HorseMessage result = await reader.Read(ms);

        Assert.Null(result);
    }

    [Fact]
    public async Task TruncatedHeader_ReturnsNull()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("trhdr");
        msg.AddHeader("LongKey", "LongValue-" + new string('X', 100));

        byte[] fullData = HorseProtocolWriter.Create(msg);

        // Truncate in the header area (keep frame but cut header)
        int headerStart = 8 + 5 + 2; // roughly after frame + id + target
        byte[] truncated = new byte[headerStart + 5];
        Array.Copy(fullData, truncated, truncated.Length);

        HorseProtocolReader reader = new HorseProtocolReader();
        using MemoryStream ms = new MemoryStream(truncated);

        HorseMessage result = await reader.Read(ms);

        Assert.Null(result);
    }

    #endregion

    #region SlowStream (fragmented reads)

    [Fact]
    public async Task SlowStream_SingleByteReads()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("slow");
        msg.SetStringContent("slow stream test");

        byte[] data = HorseProtocolWriter.Create(msg);

        // Use a stream that returns 1 byte at a time
        using SlowStream slow = new SlowStream(data, 1);
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read = await reader.Read(slow);

        Assert.NotNull(read);
        Assert.Equal("slow", read.MessageId);
        Assert.Equal("slow stream test", read.ToString());
    }

    [Fact]
    public async Task SlowStream_ThreeByteChunks()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetMessageId("chunk3");
        msg.AddHeader("Key", "Value");
        msg.SetStringContent("chunked data");

        byte[] data = HorseProtocolWriter.Create(msg);
        using SlowStream slow = new SlowStream(data, 3);
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read = await reader.Read(slow);

        Assert.NotNull(read);
        Assert.Equal("chunk3", read.MessageId);
        Assert.Equal("Value", read.FindHeader("Key"));
        Assert.Equal("chunked data", read.ToString());
    }

    #endregion

    #region Content Position Reset

    [Fact]
    public async Task ContentPosition_ResetToZero()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("pos");
        msg.SetStringContent("position test");

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.NotNull(read.Content);
        Assert.Equal(0, read.Content.Position);
    }

    [Fact]
    public async Task AdditionalContentPosition_ResetToZero()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("apos");
        msg.SetStringContent("main");
        msg.SetStringAdditionalContent("additional");
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.NotNull(read.AdditionalContent);
        Assert.Equal(0, read.AdditionalContent.Position);
    }

    #endregion
}

/// <summary>
/// A test stream that simulates slow/fragmented network reads
/// by returning at most N bytes per ReadAsync call.
/// </summary>
internal class SlowStream : Stream
{
    private readonly byte[] _data;
    private readonly int _maxBytesPerRead;
    private int _position;

    public SlowStream(byte[] data, int maxBytesPerRead)
    {
        _data = data;
        _maxBytesPerRead = maxBytesPerRead;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _data.Length;
    public override long Position
    {
        get => _position;
        set => _position = (int)value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int available = _data.Length - _position;
        if (available <= 0) return 0;

        int toRead = Math.Min(Math.Min(count, _maxBytesPerRead), available);
        Array.Copy(_data, _position, buffer, offset, toRead);
        _position += toRead;
        return toRead;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
    {
        int available = _data.Length - _position;
        if (available <= 0) return new ValueTask<int>(0);

        int toRead = Math.Min(Math.Min(buffer.Length, _maxBytesPerRead), available);
        _data.AsMemory(_position, toRead).CopyTo(buffer);
        _position += toRead;
        return new ValueTask<int>(toRead);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
    {
        int result = Read(buffer, offset, count);
        return Task.FromResult(result);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

