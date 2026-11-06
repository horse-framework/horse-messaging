using System.Text;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

public class ProtocolWriterCompatibilityTest
{
    private static readonly MessageType[] Types = Enum.GetValues<MessageType>();
    private static readonly string[] Tokens = ["a", "Z", "9", "-", "ş", "ğ", "界", "🐎"];

    [Theory]
    [InlineData(17)]
    [InlineData(20261105)]
    public async Task ValidRandomFrames_MatchLegacyWireBytes_AndRoundTrip(int seed)
    {
        Random random = new(seed);
        for (int sample = 0; sample < 2560; sample++)
        {
            using HorseMessage message = Build(random, random.Next(0, 2049), sample);
            List<KeyValuePair<string, string>> extra = Extras(message, random, sample);
            byte[] current = HorseProtocolWriter.Create(message, extra);
            Assert.Equal(0, message.Content.Position);
            byte[] legacy = LegacyProtocolWriter.Create(message, extra);
            Assert.True(legacy.AsSpan().SequenceEqual(current), $"Wire difference at seed={seed}, sample={sample}, type={message.Type}");

            if (sample % 16 == 0)
                await AssertRoundTrip(message, current, extra);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(251)]
    [InlineData(252)]
    [InlineData(253)]
    [InlineData(254)]
    [InlineData(65534)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(1048576)]
    public async Task LengthBoundaries_AllContentBufferModes_MatchLegacy(int length)
    {
        Random random = new(917 + length);
        for (int mode = 0; mode < 8; mode++)
        {
            using HorseMessage message = Build(random, length, mode);
            List<KeyValuePair<string, string>> extra = Extras(message, random, mode);
            byte[] actual = HorseProtocolWriter.Create(message, extra);
            byte[] expected = LegacyProtocolWriter.Create(message, extra);
            Assert.Equal(expected, actual);
            await AssertRoundTrip(message, actual, extra);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(253)]
    [InlineData(65536)]
    public void Write_SeekableDestinationAtOffset_PreservesPrefixAndMatchesLegacy(int length)
    {
        Random random = new(720 + length);
        using HorseMessage message = Build(random, length, 3);
        List<KeyValuePair<string, string>> extra = Extras(message, random, 3);
        byte[] prefix = Encoding.ASCII.GetBytes("existing-record-prefix");
        using MemoryStream expected = new();
        using MemoryStream actual = new();
        expected.Write(prefix);
        actual.Write(prefix);

        LegacyProtocolWriter.Write(message, expected, extra);
        HorseProtocolWriter.Write(message, actual, extra);

        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(expected.Position, actual.Position);
        Assert.Equal(prefix, actual.ToArray().AsSpan(0, prefix.Length).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_NonSeekableDestination_MatchesCreateAndRoundTrips(bool additionalOnly)
    {
        using HorseMessage message = new(MessageType.Transaction, "事务-🐎", 65535);
        if (!additionalOnly)
            message.SetStringContent("main-ş-content");
        message.SetStringAdditionalContent("additional-界-content");
        message.AddHeader("user-header", "value-🐎");
        var extra = new List<KeyValuePair<string, string>> { new("extra", "é") };
        using NonSeekableOutput destination = new();

        HorseProtocolWriter.Write(message, destination, extra);
        byte[] actual = destination.ToArray();
        Assert.Equal(HorseProtocolWriter.Create(message, extra), actual);
        await AssertRoundTrip(message, actual, extra);
    }

    [Fact]
    public async Task ConcatenatedFrames_ReadExactlyOneFrameAtATime()
    {
        Random random = new(88130);
        using MemoryStream transport = new();
        List<HorseMessage> messages = new();
        List<long> boundaries = new();
        try
        {
            for (int i = 0; i < 128; i++)
            {
                HorseMessage message = Build(random, new[] { 0, 1, 252, 253, 65535, 65536 }[i % 6], i);
                messages.Add(message);
                transport.Write(HorseProtocolWriter.Create(message));
                boundaries.Add(transport.Position);
            }
            transport.Position = 0;
            HorseProtocolReader reader = new();
            for (int i = 0; i < messages.Count; i++)
            {
                using HorseMessage read = await reader.Read(transport);
                Assert.NotNull(read);
                Assert.Equal(messages[i].MessageId ?? "", read.MessageId ?? "");
                Assert.Equal(messages[i].Content?.ToArray() ?? [], read.Content?.ToArray() ?? []);
                Assert.Equal(messages[i].AdditionalContent?.ToArray() ?? [], read.AdditionalContent?.ToArray() ?? []);
                Assert.Equal(boundaries[i], transport.Position);
            }
            Assert.Null(await reader.Read(transport));
        }
        finally
        {
            foreach (HorseMessage message in messages) message.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCreate_StableSharedMessage_ProducesIdenticalFramesAndPreservesLegacyPositions(bool exposeBuffer)
    {
        byte[] backing = new byte[65536 + 32];
        new Random(781).NextBytes(backing);
        using HorseMessage message = new(MessageType.Channel, "channel-🐎", 490);
        message.SetMessageId("id-ç");
        message.SetSource("source-界");
        message.Content = new MemoryStream(backing, 13, 65536, false, exposeBuffer);
        message.Content.Position = 43;
        message.AddHeader("header", new string('ğ', 1400));
        message.SetStringAdditionalContent("secondary-🐎");
        message.AdditionalContent.Position = 5;
        message.CalculateLengths();
        byte[] expected = HorseProtocolWriter.Create(message);
        Task[] workers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
                Assert.Equal(expected, HorseProtocolWriter.Create(message));
        })).ToArray();
        await Task.WhenAll(workers);
        Assert.Equal(0, message.Content.Position);
        Assert.Equal(5, message.AdditionalContent.Position);
        await AssertRoundTrip(message, expected, null);
    }

    private static HorseMessage Build(Random random, int length, int mode)
    {
        HorseMessage message = new(Types[mode % Types.Length], Text(random, random.Next(0, 18)), (ushort)random.Next(0, 65536))
        {
            WaitResponse = (mode & 1) != 0,
            HighPriority = (mode & 2) != 0
        };
        message.SetMessageId(Text(random, random.Next(0, 18)));
        message.SetSource(Text(random, random.Next(0, 18)));
        byte[] backing = new byte[length + 37];
        random.NextBytes(backing);
        message.Content = new MemoryStream(backing, (mode & 1) == 0 ? 0 : 17, length, (mode & 2) != 0, (mode & 4) != 0);
        message.Content.Position = length == 0 ? 0 : random.Next(length + 1);
        if (length > 0 && mode % 3 == 0)
            message.SetStringAdditionalContent(Text(random, random.Next(1, 80)));
        int headers = message.Type is MessageType.Ping or MessageType.Pong ? 0 : mode % 5;
        for (int i = 0; i < headers; i++)
            message.AddHeader("key-" + i + Text(random, 3), Text(random, random.Next(0, mode % 11 == 0 ? 1200 : 80)));
        if ((mode & 1) == 0)
            message.CalculateLengths();
        return message;
    }

    private static List<KeyValuePair<string, string>> Extras(HorseMessage message, Random random, int sample)
    {
        if (sample % 4 == 0 || message.Type is MessageType.Ping or MessageType.Pong) return null;
        List<KeyValuePair<string, string>> extra = new();
        for (int i = 0; i < sample % 4; i++)
            extra.Add(new("extra-" + i, Text(random, random.Next(0, 80))));
        return extra;
    }

    private static string Text(Random random, int tokens)
    {
        StringBuilder builder = new();
        for (int i = 0; i < tokens; i++)
            builder.Append(Tokens[random.Next(Tokens.Length)]);
        return builder.ToString();
    }

    private static async Task AssertRoundTrip(HorseMessage expected, byte[] bytes, List<KeyValuePair<string, string>> extra)
    {
        using MemoryStream input = new(bytes);
        using HorseMessage read = await new HorseProtocolReader().Read(input);
        Assert.NotNull(read);
        Assert.Equal(expected.Type, read.Type);
        Assert.Equal(expected.ContentType, read.ContentType);
        Assert.Equal(expected.WaitResponse, read.WaitResponse);
        Assert.Equal(expected.HighPriority, read.HighPriority);
        Assert.Equal(expected.MessageId ?? "", read.MessageId ?? "");
        Assert.Equal(expected.Source ?? "", read.Source ?? "");
        Assert.Equal(expected.Target ?? "", read.Target ?? "");
        Assert.Equal(expected.Content?.ToArray() ?? [], read.Content?.ToArray() ?? []);
        Assert.Equal(expected.AdditionalContent?.ToArray() ?? [], read.AdditionalContent?.ToArray() ?? []);
        foreach (var pair in expected.Headers ?? [])
            Assert.Equal(pair.Value, read.FindHeader(pair.Key));
        foreach (var pair in extra ?? [])
            Assert.Equal(pair.Value, read.FindHeader(pair.Key));
        Assert.Equal(input.Length, input.Position);
    }

    private sealed class NonSeekableOutput : Stream
    {
        private readonly MemoryStream _inner = new();
        public byte[] ToArray() => _inner.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
