using System.Buffers.Binary;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

public class ProtocolWriterDerivedStreamTest
{
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    public void Create_DerivedPrimaryStream_HonorsVirtualWriteTo(bool exposeBuffer, int emittedLength)
    {
        byte[] emitted = Enumerable.Range(4, emittedLength).Select(value => (byte)value).ToArray();
        using var content = new EmittingStream(exposeBuffer, emitted);
        var message = new HorseMessage(MessageType.Server) { Content = content };
        byte[] expected = new byte[] { 16, 0, 0, 0, 0, 0, 0, 3 }.Concat(emitted).ToArray();

        Assert.Equal(expected, HorseProtocolWriter.Create(message));
        using var output = new MemoryStream();
        HorseProtocolWriter.Write(message, output);
        Assert.Equal(expected, output.ToArray());
        Assert.Equal(2, content.WriteCalls);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    public void Create_DerivedAdditionalStream_HonorsVirtualWriteTo(bool exposeBuffer, int emittedLength)
    {
        byte[] emitted = Enumerable.Range(4, emittedLength).Select(value => (byte)value).ToArray();
        using var content = new EmittingStream(exposeBuffer, emitted);
        var message = new HorseMessage(MessageType.Server);
        message.SetStringContent("p");
        message.SetStringAdditionalContent("abc");
        message.AdditionalContent = content;
        byte[] expected = new byte[] { 16, 128, 0, 0, 0, 0, 0, 1, 3, 0, 0, 0, (byte)'p' }
            .Concat(emitted).ToArray();

        Assert.Equal(expected, HorseProtocolWriter.Create(message));
        using var output = new MemoryStream();
        HorseProtocolWriter.Write(message, output);
        Assert.Equal(expected, output.ToArray());
        Assert.Equal(2, content.WriteCalls);
    }

    [Theory]
    [InlineData(4294967295L, 254)]
    [InlineData(4294967296L, 255)]
    [InlineData(long.MaxValue, 255)]
    public void Create_DerivedLength_EncodesWideFrameWithoutAllocatingReportedPayload(long reportedLength, byte marker)
    {
        // Only the declared frame length is under test: the custom stream emits
        // three bytes so covering the 64-bit encoding does not allocate 4+ GiB.
        using var content = new ReportedLengthStream(reportedLength);
        var message = new HorseMessage(MessageType.Server) { Content = content };

        byte[] data = HorseProtocolWriter.Create(message);
        Assert.Equal(marker, data[7]);
        ulong declared = marker == 254
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4))
            : BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(8, 8));
        Assert.Equal((ulong)reportedLength, declared);
        Assert.Equal(new byte[] { 1, 2, 3 }, data[^3..]);
        Assert.Equal(marker == 254 ? 15 : 19, data.Length);
    }

    private sealed class EmittingStream(bool exposeBuffer, byte[] emitted)
        : MemoryStream(new byte[] { 1, 2, 3 }, 0, 3, false, exposeBuffer)
    {
        internal int WriteCalls;

        public override void WriteTo(Stream destination)
        {
            WriteCalls++;
            destination.Write(emitted);
        }
    }

    private sealed class ReportedLengthStream(long reportedLength) : MemoryStream(new byte[] { 1, 2, 3 })
    {
        public override long Length => reportedLength;
    }
}
