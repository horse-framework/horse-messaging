using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests for HorseProtocolWriter: frame encoding, header writing, content writing.
/// Validates the binary protocol format is correctly produced.
/// </summary>
public class ProtocolWriterTest
{
    #region Create byte[]

    [Fact]
    public void Create_MinimalMessage_ProducesBytes()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "t");
        msg.SetMessageId("a");

        byte[] data = HorseProtocolWriter.Create(msg);

        Assert.NotNull(data);
        Assert.True(data.Length >= 8); // Minimum frame size
    }

    [Fact]
    public void Create_EmptyMessage_ProducesValidFrame()
    {
        HorseMessage msg = new HorseMessage(MessageType.Ping);

        byte[] data = HorseProtocolWriter.Create(msg);

        Assert.NotNull(data);
        Assert.True(data.Length >= 8);
    }

    [Fact]
    public void Create_WithContent_IncludesContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("x");
        msg.SetStringContent("Hello");

        byte[] data = HorseProtocolWriter.Create(msg);

        Assert.NotNull(data);
        // data must be larger than just frame
        Assert.True(data.Length > 10);
    }

    #endregion

    #region Write to Stream

    [Fact]
    public void Write_ToSeekableStream()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("w-001");
        msg.SetStringContent("stream content");

        using MemoryStream ms = new MemoryStream();
        HorseProtocolWriter.Write(msg, ms);

        Assert.True(ms.Length > 0);
    }

    [Fact]
    public void Write_ProducesSameAsCreate()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("cmp-001");
        msg.SetStringContent("compare test");

        byte[] created = HorseProtocolWriter.Create(msg);

        // Reset content position for Write
        msg.Content.Position = 0;
        using MemoryStream ms = new MemoryStream();
        HorseProtocolWriter.Write(msg, ms);
        byte[] written = ms.ToArray();

        Assert.Equal(created, written);
    }

    #endregion

    #region Frame Encoding

    [Fact]
    public void FrameByte0_EncodesMessageType()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage);

        byte[] data = HorseProtocolWriter.Create(msg);

        // MessageType.QueueMessage = 0x11 = 17
        byte proto = data[0];
        Assert.Equal((byte)MessageType.QueueMessage, proto);
    }

    [Fact]
    public void FrameByte0_EncodesWaitResponse()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage);
        msg.WaitResponse = true;

        byte[] data = HorseProtocolWriter.Create(msg);

        byte proto = data[0];
        Assert.True(proto >= 128);
    }

    [Fact]
    public void FrameByte0_EncodesHighPriority()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage);
        msg.HighPriority = true;

        byte[] data = HorseProtocolWriter.Create(msg);

        byte proto = data[0];
        Assert.True(proto >= 64);
    }

    [Fact]
    public void FrameByte0_EncodesHeaderFlag()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage);
        msg.AddHeader("Key", "Value");

        byte[] data = HorseProtocolWriter.Create(msg);

        byte proto = data[0];
        // Header flag adds 32 to the protocol byte
        // QueueMessage = 0x11 = 17, with header = 17 + 32 = 49
        Assert.Equal((byte)MessageType.QueueMessage + 32, proto);
    }

    [Fact]
    public void FrameByte0_EncodesAllFlags()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage);
        msg.WaitResponse = true;
        msg.HighPriority = true;
        msg.AddHeader("K", "V");

        byte[] data = HorseProtocolWriter.Create(msg);

        byte proto = data[0];
        byte expected = (byte)((byte)MessageType.QueueMessage + 128 + 64 + 32);
        Assert.Equal(expected, proto);
    }

    #endregion

    #region Content Length Encoding

    [Fact]
    public async Task SmallLength_SingleByte()
    {
        // Content < 253 bytes: single byte encoding
        byte[] content = new byte[100];
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("sl");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.Equal(100ul, read.Length);
    }

    [Fact]
    public async Task MediumLength_TwoByteMarker253()
    {
        // Content 253-65535: marker byte 253 + 2 byte length
        byte[] content = new byte[500];
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("ml");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.Equal(500ul, read.Length);
    }

    [Fact]
    public async Task LargeLength_FourByteMarker254()
    {
        // Content > 65535: marker byte 254 + 4 byte length
        byte[] content = new byte[70000];
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("ll");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.Equal(70000ul, read.Length);
    }

    #endregion

    #region Additional Headers in Write

    [Fact]
    public async Task AdditionalHeaders_IncludedInOutput()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("ah-001");
        msg.AddHeader("Original", "Header");

        var additional = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
        {
            new("Extra", "Value")
        };

        byte[] data = HorseProtocolWriter.Create(msg, additional);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("Header", read.FindHeader("Original"));
        Assert.Equal("Value", read.FindHeader("Extra"));
    }

    #endregion
}


