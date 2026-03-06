using System.Threading;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests for predefined protocol bytes: handshake, ping, pong.
/// </summary>
public class PredefinedMessagesTest
{
    [Fact]
    public void ProtocolBytesV3_CorrectContent()
    {
        byte[] expected = "HORSE/30"u8.ToArray();
        Assert.Equal(expected, PredefinedMessages.PROTOCOL_BYTES_V3);
    }

    [Fact]
    public void ProtocolBytesV4_CorrectContent()
    {
        byte[] expected = "HORSE/40"u8.ToArray();
        Assert.Equal(expected, PredefinedMessages.PROTOCOL_BYTES_V4);
    }

    [Fact]
    public void ProtocolBytes_Length8()
    {
        Assert.Equal(8, PredefinedMessages.PROTOCOL_BYTES_V3.Length);
        Assert.Equal(8, PredefinedMessages.PROTOCOL_BYTES_V4.Length);
    }

    [Fact]
    public void PingMessage_CorrectFormat()
    {
        Assert.Equal(8, PredefinedMessages.PING.Length);
        Assert.Equal(0x89, PredefinedMessages.PING[0]);
        // rest should be zeros
        for (int i = 1; i < 8; i++)
            Assert.Equal(0x00, PredefinedMessages.PING[i]);
    }

    [Fact]
    public void PongMessage_CorrectFormat()
    {
        Assert.Equal(8, PredefinedMessages.PONG.Length);
        Assert.Equal(0x8A, PredefinedMessages.PONG[0]);
        for (int i = 1; i < 8; i++)
            Assert.Equal(0x00, PredefinedMessages.PONG[i]);
    }

    [Fact]
    public void PingAndPong_AreDifferent()
    {
        Assert.NotEqual(PredefinedMessages.PING, PredefinedMessages.PONG);
    }

    [Fact]
    public void ProtocolV3AndV4_AreDifferent()
    {
        Assert.NotEqual(PredefinedMessages.PROTOCOL_BYTES_V3, PredefinedMessages.PROTOCOL_BYTES_V4);
    }

    [Fact]
    public void ProtocolBytes_AreASCIICompatible()
    {
        string v3 = System.Text.Encoding.ASCII.GetString(PredefinedMessages.PROTOCOL_BYTES_V3);
        string v4 = System.Text.Encoding.ASCII.GetString(PredefinedMessages.PROTOCOL_BYTES_V4);

        Assert.Equal("HORSE/30", v3);
        Assert.Equal("HORSE/40", v4);
    }
}

