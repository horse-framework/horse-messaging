using System.Text;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

public class ProtocolWriterBufferTest
{
    [Fact]
    public void Create_AllFrameFields_MatchesKnownWireBytes()
    {
        var message = new HorseMessage(MessageType.QueueMessage, "t", 258)
        {
            WaitResponse = true,
            HighPriority = true
        };
        message.SetMessageId("i");
        message.SetSource("s");
        message.SetStringContent("abc");
        message.SetStringAdditionalContent("de");
        message.AddHeader("K", "V");
        var extra = new List<KeyValuePair<string, string>> { new("E", "X") };
        byte[] expected =
        [
            0xf1, 0x80, 1, 1, 1, 2, 1, 3, 2, 0, 0, 0,
            (byte)'i', (byte)'s', (byte)'t', 10, 0,
            (byte)'K', (byte)':', (byte)'V', 13, 10,
            (byte)'E', (byte)':', (byte)'X', 13, 10,
            (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e'
        ];

        Assert.Equal(expected, HorseProtocolWriter.Create(message, extra));
        using var written = new MemoryStream();
        HorseProtocolWriter.Write(message, written, extra);
        Assert.Equal(expected, written.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(252)]
    [InlineData(253)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(1048576)]
    public async Task Create_LengthBoundaries_PreservesExactPayloadAndMatchesStreamWriter(int length)
    {
        byte[] payload = new byte[length];
        new Random(42).NextBytes(payload);
        var message = new HorseMessage(MessageType.Response, "cache-key", 17)
        {
            // An opaque stream exercises WriteTo without exposing or copying
            // its backing array into another payload-sized buffer.
            Content = new MemoryStream(payload)
        };
        message.SetMessageId("response-id");
        message.AddHeader("tag", "first,second");

        byte[] data = HorseProtocolWriter.Create(message);
        using var written = new MemoryStream();
        HorseProtocolWriter.Write(message, written);
        Assert.Equal(data, written.ToArray());

        using var input = new MemoryStream(data);
        var read = await new HorseProtocolReader().Read(input);
        Assert.NotNull(read);
        Assert.Equal((ulong)length, read.Length);
        Assert.Equal(payload, read.Content?.ToArray() ?? []);
        Assert.Equal(input.Length, input.Position);
    }

    [Fact]
    public async Task Create_OffsetBufferAndUnicodeMetadata_RoundTrips()
    {
        byte[] backing = Encoding.UTF8.GetBytes("prefix-payload-suffix");
        using var content = new MemoryStream(backing, 7, 7, false, true);
        content.Position = 3;
        var message = new HorseMessage(MessageType.QueueMessage, "kuyruk-🐎") { Content = content };
        message.SetMessageId("kimlik-ş");
        message.SetSource("kaynak-ç");
        message.AddHeader("başlık", "değer-🐎");
        var extra = new List<KeyValuePair<string, string>> { new("ek", new string('ğ', 2000)) };

        byte[] data = HorseProtocolWriter.Create(message, extra);
        using var input = new MemoryStream(data);
        var read = await new HorseProtocolReader().Read(input);

        Assert.NotNull(read);
        Assert.Equal("payload", read.ToString());
        Assert.Equal(message.MessageId, read.MessageId);
        Assert.Equal(message.Source, read.Source);
        Assert.Equal(message.Target, read.Target);
        Assert.Equal("değer-🐎", read.FindHeader("başlık"));
        Assert.Equal(extra[0].Value, read.FindHeader("ek"));
        Assert.Equal(0, content.Position);
        Assert.Equal(input.Length, input.Position);
    }

    [Fact]
    public async Task Create_CombinedIdentifiersExceed256Bytes_RoundTrips()
    {
        var message = new HorseMessage(MessageType.Server, new string('t', 255));
        message.SetMessageId(new string('i', 255));
        message.SetSource(new string('s', 255));
        byte[] data = HorseProtocolWriter.Create(message);

        using var input = new MemoryStream(data);
        var read = await new HorseProtocolReader().Read(input);
        Assert.NotNull(read);
        Assert.Equal(message.MessageId, read.MessageId);
        Assert.Equal(message.Source, read.Source);
        Assert.Equal(message.Target, read.Target);

        using var written = new MemoryStream();
        HorseProtocolWriter.Write(message, written);
        Assert.Equal(data, written.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_AdditionalContent_RoundTripsWithOrWithoutPrimaryContent(bool hasPrimaryContent)
    {
        var message = new HorseMessage(MessageType.Server, "target");
        if (hasPrimaryContent)
            message.SetStringContent("primary");
        message.SetStringAdditionalContent("additional-🐎");

        byte[] data = HorseProtocolWriter.Create(message);
        using var written = new MemoryStream();
        HorseProtocolWriter.Write(message, written);
        Assert.Equal(data, written.ToArray());

        using var input = new MemoryStream(data);
        var read = await new HorseProtocolReader().Read(input);
        Assert.NotNull(read);
        Assert.Equal(hasPrimaryContent ? "primary" : "", read.ToString());
        Assert.True(read.HasAdditionalContent);
        Assert.Equal("additional-🐎", Encoding.UTF8.GetString(read.AdditionalContent.ToArray()));
        Assert.Equal(input.Length, input.Position);
    }

    [Fact]
    public async Task Create_MaximumHeaderSize_RoundTrips()
    {
        var message = new HorseMessage(MessageType.Server);
        message.AddHeader("k", new string('v', ushort.MaxValue - 4));
        byte[] data = HorseProtocolWriter.Create(message);

        using var input = new MemoryStream(data);
        var read = await new HorseProtocolReader().Read(input);
        Assert.NotNull(read);
        Assert.Equal(message.FindHeader("k"), read.FindHeader("k"));
        Assert.Equal(input.Length, input.Position);
    }

    [Fact]
    public void Create_OverflowingHeaderOrIdentifier_RejectsInvalidWireLengths()
    {
        var message = new HorseMessage(MessageType.Server);
        message.AddHeader("k", new string('v', ushort.MaxValue - 3));
        Assert.Throws<ArgumentException>(() => HorseProtocolWriter.Create(message));
        using var output = new MemoryStream();
        Assert.Throws<ArgumentException>(() => HorseProtocolWriter.Write(message, output));
        Assert.Equal(0, output.Length);

        message = new HorseMessage(MessageType.Server, new string('ğ', 128));
        Assert.Throws<ArgumentException>(() => HorseProtocolWriter.Create(message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_LargePayload_AllocatesOnlyOnePayloadSizedBuffer(bool exposeBuffer)
    {
        const int payloadLength = 1024 * 1024;
        const int iterations = 16;
        byte[] payload = new byte[payloadLength];
        var message = new HorseMessage(MessageType.Response, "cache-key")
        {
            Content = new MemoryStream(payload, 0, payload.Length, false, exposeBuffer)
        };
        message.AddHeader("tag", "first,second");
        for (int i = 0; i < 16; i++)
            HorseProtocolWriter.Create(message);

        long before = GC.GetAllocatedBytesForCurrentThread();
        int wireLength = 0;
        for (int i = 0; i < iterations; i++)
            wireLength = HorseProtocolWriter.Create(message).Length;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, (long)wireLength * iterations, (long)(wireLength + 256) * iterations);
    }
}
