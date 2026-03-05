using System.Threading;
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

    #region State Isolation — No Leakage Between Messages

    [Fact]
    public async Task Reader_HeaderState_DoesNotLeakBetweenMessages()
    {
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage msg1 = new HorseMessage(MessageType.Server, "target");
        msg1.SetMessageId("iso-001");
        msg1.AddHeader("Secret", "should-not-leak");
        msg1.SetStringContent("with headers");

        HorseMessage msg2 = new HorseMessage(MessageType.QueueMessage, "queue");
        msg2.SetMessageId("iso-002");
        msg2.SetStringContent("no headers");

        byte[] data1 = HorseProtocolWriter.Create(msg1);
        byte[] data2 = HorseProtocolWriter.Create(msg2);

        using MemoryStream ms = new MemoryStream();
        ms.Write(data1);
        ms.Write(data2);
        ms.Position = 0;

        HorseMessage read1 = await reader.Read(ms);
        HorseMessage read2 = await reader.Read(ms);

        Assert.NotNull(read1);
        Assert.True(read1.HasHeader);
        Assert.Equal("should-not-leak", read1.FindHeader("Secret"));

        Assert.NotNull(read2);
        Assert.False(read2.HasHeader);
        Assert.Null(read2.FindHeader("Secret"));
    }

    [Fact]
    public async Task Reader_AdditionalContent_DoesNotLeakBetweenMessages()
    {
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage msg1 = new HorseMessage(MessageType.QueueMessage, "q");
        msg1.SetMessageId("acl-001");
        msg1.SetStringContent("main");
        msg1.SetStringAdditionalContent("additional");
        msg1.CalculateLengths();

        HorseMessage msg2 = new HorseMessage(MessageType.QueueMessage, "q");
        msg2.SetMessageId("acl-002");
        msg2.SetStringContent("only main");

        byte[] data1 = HorseProtocolWriter.Create(msg1);
        byte[] data2 = HorseProtocolWriter.Create(msg2);

        using MemoryStream ms = new MemoryStream();
        ms.Write(data1);
        ms.Write(data2);
        ms.Position = 0;

        HorseMessage read1 = await reader.Read(ms);
        HorseMessage read2 = await reader.Read(ms);

        Assert.NotNull(read1);
        Assert.True(read1.HasAdditionalContent);

        Assert.NotNull(read2);
        Assert.False(read2.HasAdditionalContent);
        Assert.Null(read2.AdditionalContent);
    }

    [Fact]
    public async Task Reader_Alternating_HeadersAndNoHeaders_10Messages()
    {
        HorseProtocolReader reader = new HorseProtocolReader();
        using MemoryStream ms = new MemoryStream();

        for (int i = 0; i < 10; i++)
        {
            HorseMessage msg = new HorseMessage(MessageType.Server, "t");
            msg.SetMessageId($"alt-{i:D2}");

            if (i % 2 == 0)
                msg.AddHeader("Index", i.ToString());

            msg.SetStringContent($"body-{i}");
            byte[] data = HorseProtocolWriter.Create(msg);
            ms.Write(data);
        }

        ms.Position = 0;

        for (int i = 0; i < 10; i++)
        {
            HorseMessage read = await reader.Read(ms);
            Assert.NotNull(read);
            Assert.Equal($"alt-{i:D2}", read.MessageId);
            Assert.Equal($"body-{i}", read.ToString());

            if (i % 2 == 0)
            {
                Assert.True(read.HasHeader);
                Assert.Equal(i.ToString(), read.FindHeader("Index"));
            }
            else
            {
                Assert.False(read.HasHeader);
            }
        }
    }

    #endregion

    #region SlowStream Advanced

    [Fact]
    public async Task SlowStream_SingleByte_FullMessage_WithAllFields()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target", 777);
        msg.SetMessageId("slow-full");
        msg.SetSource("source");
        msg.WaitResponse = true;
        msg.AddHeader("H1", "V1");
        msg.SetStringContent("slow content");
        msg.SetStringAdditionalContent("slow additional");
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using SlowStream slow = new SlowStream(data, 1);
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read = await reader.Read(slow);

        Assert.NotNull(read);
        Assert.Equal("slow-full", read.MessageId);
        Assert.Equal("source", read.Source);
        Assert.Equal("target", read.Target);
        Assert.Equal(777, read.ContentType);
        Assert.True(read.WaitResponse);
        Assert.True(read.HasHeader);
        Assert.Equal("V1", read.FindHeader("H1"));
        Assert.Equal("slow content", read.ToString());
        Assert.True(read.HasAdditionalContent);
    }

    [Fact]
    public async Task SlowStream_LargeContent_FiveByteChunks()
    {
        byte[] content = new byte[10000];
        Random.Shared.NextBytes(content);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("slow-lg");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using SlowStream slow = new SlowStream(data, 5);
        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read = await reader.Read(slow);

        Assert.NotNull(read);
        Assert.Equal(10000ul, read.Length);
        Assert.Equal(content, read.Content.ToArray());
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

