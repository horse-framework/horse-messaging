using System.Text;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests for HorseProtocolWriter.Create() and HorseProtocolReader.Read() round-trip.
/// Ensures messages survive serialization → deserialization without data loss.
/// </summary>
public class ProtocolReadWriteTest
{
    private static async Task<HorseMessage> WriteAndRead(HorseMessage original)
    {
        byte[] data = HorseProtocolWriter.Create(original);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        return await reader.Read(ms);
    }

    #region Basic Message Types

    [Theory]
    [InlineData(MessageType.Server)]
    [InlineData(MessageType.QueueMessage)]
    [InlineData(MessageType.DirectMessage)]
    [InlineData(MessageType.Response)]
    [InlineData(MessageType.QueuePullRequest)]
    [InlineData(MessageType.Channel)]
    [InlineData(MessageType.Cache)]
    [InlineData(MessageType.Event)]
    [InlineData(MessageType.Transaction)]
    [InlineData(MessageType.Cluster)]
    [InlineData(MessageType.Plugin)]
    public async Task MessageTypePreserved(MessageType type)
    {
        HorseMessage msg = new HorseMessage(type, "test-target");
        msg.SetMessageId("msg-001");
        msg.ContentType = 100;

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(type, read.Type);
        Assert.Equal("test-target", read.Target);
        Assert.Equal("msg-001", read.MessageId);
        Assert.Equal(100, read.ContentType);
    }

    [Fact]
    public async Task PingMessageRoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.Ping);

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(MessageType.Ping, read.Type);
    }

    [Fact]
    public async Task PongMessageRoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.Pong);

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(MessageType.Pong, read.Type);
    }

    #endregion

    #region Flags

    [Fact]
    public async Task WaitResponseFlagPreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target-1");
        msg.WaitResponse = true;
        msg.SetMessageId("wr-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.WaitResponse);
    }

    [Fact]
    public async Task HighPriorityFlagPreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.HighPriority = true;
        msg.SetMessageId("hp-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HighPriority);
    }

    [Fact]
    public async Task BothFlagsPreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target-1");
        msg.WaitResponse = true;
        msg.HighPriority = true;
        msg.SetMessageId("bf-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.WaitResponse);
        Assert.True(read.HighPriority);
    }

    [Fact]
    public async Task NoFlagsPreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target-1");
        msg.WaitResponse = false;
        msg.HighPriority = false;
        msg.SetMessageId("nf-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.False(read.WaitResponse);
        Assert.False(read.HighPriority);
    }

    #endregion

    #region Content Sizes

    [Fact]
    public async Task EmptyContentMessage()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetMessageId("ec-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(0ul, read.Length);
        Assert.Null(read.Content);
    }

    [Fact]
    public async Task SmallContent_Under253Bytes()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("sc-001");
        msg.SetStringContent("Hello, Horse!");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal("Hello, Horse!", read.ToString());
        Assert.True(read.Length < 253);
    }

    [Fact]
    public async Task MediumContent_UInt16Range()
    {
        // 1000 bytes = needs 2 byte length (253 marker)
        string content = new string('A', 1000);
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("mc-001");
        msg.SetStringContent(content);

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(content, read.ToString());
        Assert.Equal((ulong)Encoding.UTF8.GetByteCount(content), read.Length);
    }

    [Fact]
    public async Task LargeContent_UInt32Range()
    {
        // 70000 bytes = exceeds ushort.MaxValue threshold check (needs 4 byte length marker 254)
        string content = new string('B', 70000);
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("lc-001");
        msg.SetStringContent(content);

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(content, read.ToString());
    }

    [Fact]
    public async Task ExactBoundary_252Bytes()
    {
        // 252 bytes - max for single byte length encoding
        byte[] contentBytes = new byte[252];
        Random.Shared.NextBytes(contentBytes);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("eb-001");
        msg.Content = new MemoryStream(contentBytes);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(252ul, read.Length);
    }

    [Fact]
    public async Task ExactBoundary_253Bytes()
    {
        // 253 bytes - triggers 2-byte length encoding
        byte[] contentBytes = new byte[253];
        Random.Shared.NextBytes(contentBytes);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("eb-002");
        msg.Content = new MemoryStream(contentBytes);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(253ul, read.Length);
    }

    [Fact]
    public async Task BinaryContent_PreservedExactly()
    {
        byte[] original = new byte[512];
        Random.Shared.NextBytes(original);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("bc-001");
        msg.Content = new MemoryStream(original);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.NotNull(read.Content);
        byte[] readContent = read.Content.ToArray();
        Assert.Equal(original, readContent);
    }

    #endregion

    #region Headers

    [Fact]
    public async Task SingleHeader()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("sh-001");
        msg.AddHeader("X-Key", "X-Value");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("X-Value", read.FindHeader("X-Key"));
    }

    [Fact]
    public async Task MultipleHeaders()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("mh-001");
        msg.AddHeader("Key1", "Value1");
        msg.AddHeader("Key2", "Value2");
        msg.AddHeader("Key3", "Value3");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("Value1", read.FindHeader("Key1"));
        Assert.Equal("Value2", read.FindHeader("Key2"));
        Assert.Equal("Value3", read.FindHeader("Key3"));
    }

    [Fact]
    public async Task HeaderWithUnicodeCharacters()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("uh-001");
        msg.AddHeader("Başlık", "Değer-ÜÖŞ");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("Değer-ÜÖŞ", read.FindHeader("Başlık"));
    }

    [Fact]
    public async Task HeadersWithContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("hc-001");
        msg.AddHeader("Content-Type", "application/json");
        msg.SetStringContent("{\"name\":\"horse\"}");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("application/json", read.FindHeader("Content-Type"));
        Assert.Equal("{\"name\":\"horse\"}", read.ToString());
    }

    [Fact]
    public async Task NoHeaders_HasHeaderIsFalse()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetMessageId("nh-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.False(read.HasHeader);
    }

    #endregion

    #region Source, Target, MessageId

    [Fact]
    public async Task SourcePreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target-client");
        msg.SetMessageId("sp-001");
        msg.SetSource("source-client");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal("source-client", read.Source);
    }

    [Fact]
    public async Task TargetPreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "my-queue");
        msg.SetMessageId("tp-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal("my-queue", read.Target);
    }

    [Fact]
    public async Task UnicodeSourceAndTarget()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "hedef-ÜŞÖ");
        msg.SetMessageId("ut-001");
        msg.SetSource("kaynak-ÇĞİ");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal("hedef-ÜŞÖ", read.Target);
        Assert.Equal("kaynak-ÇĞİ", read.Source);
    }

    [Fact]
    public async Task ContentTypePreserved()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target", 12345);
        msg.SetMessageId("ct-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(12345, read.ContentType);
    }

    [Fact]
    public async Task MaxContentType()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target", ushort.MaxValue);
        msg.SetMessageId("mct-001");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(ushort.MaxValue, read.ContentType);
    }

    #endregion

    #region Additional Content

    [Fact]
    public async Task AdditionalContent_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("ac-001");
        msg.SetStringContent("main content");
        msg.SetStringAdditionalContent("additional content");
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasAdditionalContent);
        Assert.Equal("main content", read.ToString());
        Assert.NotNull(read.AdditionalContent);

        string additionalText = Encoding.UTF8.GetString(read.AdditionalContent.ToArray());
        Assert.Equal("additional content", additionalText);
    }

    [Fact]
    public async Task NoAdditionalContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg.SetMessageId("nac-001");
        msg.SetStringContent("only main");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.False(read.HasAdditionalContent);
        Assert.Null(read.AdditionalContent);
    }

    #endregion

    #region Multi-Message Stream

    [Fact]
    public async Task MultipleMessagesInSameStream()
    {
        HorseMessage msg1 = new HorseMessage(MessageType.QueueMessage, "queue-1");
        msg1.SetMessageId("mm-001");
        msg1.SetStringContent("First message");

        HorseMessage msg2 = new HorseMessage(MessageType.DirectMessage, "client-1");
        msg2.SetMessageId("mm-002");
        msg2.SetStringContent("Second message");

        HorseMessage msg3 = new HorseMessage(MessageType.Server, "server");
        msg3.SetMessageId("mm-003");
        msg3.AddHeader("Key", "Value");

        byte[] data1 = HorseProtocolWriter.Create(msg1);
        byte[] data2 = HorseProtocolWriter.Create(msg2);
        byte[] data3 = HorseProtocolWriter.Create(msg3);

        using MemoryStream ms = new MemoryStream();
        ms.Write(data1);
        ms.Write(data2);
        ms.Write(data3);
        ms.Position = 0;

        HorseProtocolReader reader = new HorseProtocolReader();

        HorseMessage read1 = await reader.Read(ms);
        HorseMessage read2 = await reader.Read(ms);
        HorseMessage read3 = await reader.Read(ms);

        Assert.NotNull(read1);
        Assert.Equal("mm-001", read1.MessageId);
        Assert.Equal("First message", read1.ToString());

        Assert.NotNull(read2);
        Assert.Equal("mm-002", read2.MessageId);
        Assert.Equal("Second message", read2.ToString());

        Assert.NotNull(read3);
        Assert.Equal("mm-003", read3.MessageId);
        Assert.Equal("Value", read3.FindHeader("Key"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EmptyStream_ReturnsNull()
    {
        using MemoryStream ms = new MemoryStream();
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage result = await reader.Read(ms);

        Assert.Null(result);
    }

    [Fact]
    public async Task TruncatedStream_ReturnsNull()
    {
        // Only 4 bytes, but protocol requires 8 minimum
        using MemoryStream ms = new MemoryStream([0x11, 0x00, 0x00, 0x00]);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage result = await reader.Read(ms);

        Assert.Null(result);
    }

    [Fact]
    public async Task FullMessage_AllFieldsPopulated()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target-client", 999);
        msg.SetMessageId("full-001");
        msg.SetSource("source-client");
        msg.WaitResponse = true;
        msg.HighPriority = true;
        msg.AddHeader("H1", "V1");
        msg.AddHeader("H2", "V2");
        msg.SetStringContent("complete message body");
        msg.SetStringAdditionalContent("extra data");
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(MessageType.DirectMessage, read.Type);
        Assert.Equal("target-client", read.Target);
        Assert.Equal("source-client", read.Source);
        Assert.Equal("full-001", read.MessageId);
        Assert.Equal(999, read.ContentType);
        Assert.True(read.WaitResponse);
        Assert.True(read.HighPriority);
        Assert.True(read.HasHeader);
        Assert.Equal("V1", read.FindHeader("H1"));
        Assert.Equal("V2", read.FindHeader("H2"));
        Assert.Equal("complete message body", read.ToString());
        Assert.True(read.HasAdditionalContent);
        Assert.NotNull(read.AdditionalContent);
    }

    #endregion

    #region Content Size Boundaries

    [Fact]
    public async Task ExactBoundary_UInt16Max_65535Bytes()
    {
        byte[] content = new byte[65535];
        Random.Shared.NextBytes(content);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("u16max");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(65535ul, read.Length);
        Assert.Equal(content, read.Content.ToArray());
    }

    [Fact]
    public async Task ExactBoundary_UInt16MaxPlus1_65536Bytes()
    {
        byte[] content = new byte[65536];
        Random.Shared.NextBytes(content);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("u16ovr");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(65536ul, read.Length);
        Assert.Equal(content, read.Content.ToArray());
    }

    #endregion

    #region Header Edge Cases

    [Fact]
    public async Task EmptyHeaderValue_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("ehv-001");
        msg.AddHeader("EmptyVal", "");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("", read.FindHeader("EmptyVal"));
    }

    [Fact]
    public async Task HeaderValue_WithColons_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("colon-001");
        msg.AddHeader("URL", "http://example.com:8080/path");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        string value = read.FindHeader("URL");
        Assert.NotNull(value);
        Assert.Contains("example.com", value);
    }

    [Fact]
    public async Task ManyHeaders_50_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("many-001");

        for (int i = 0; i < 50; i++)
            msg.AddHeader($"Key-{i:D3}", $"Value-{i:D3}");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        for (int i = 0; i < 50; i++)
            Assert.Equal($"Value-{i:D3}", read.FindHeader($"Key-{i:D3}"));
    }

    #endregion

    #region Combined Fields

    [Fact]
    public async Task AllFieldsCombined_Headers_Content_AdditionalContent_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-1", 42);
        msg.SetMessageId("combo-001");
        msg.SetSource("source");
        msg.WaitResponse = true;
        msg.HighPriority = true;
        msg.AddHeader("H1", "V1");
        msg.AddHeader("H2", "V2");
        msg.SetStringContent("main body");
        msg.SetStringAdditionalContent("extra body");
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(MessageType.QueueMessage, read.Type);
        Assert.Equal("queue-1", read.Target);
        Assert.Equal("source", read.Source);
        Assert.Equal("combo-001", read.MessageId);
        Assert.Equal(42, read.ContentType);
        Assert.True(read.WaitResponse);
        Assert.True(read.HighPriority);
        Assert.True(read.HasHeader);
        Assert.Equal("V1", read.FindHeader("H1"));
        Assert.Equal("V2", read.FindHeader("H2"));
        Assert.Equal("main body", read.ToString());
        Assert.True(read.HasAdditionalContent);
        string additional = Encoding.UTF8.GetString(read.AdditionalContent.ToArray());
        Assert.Equal("extra body", additional);
    }

    #endregion

    #region Zero-Length Fields

    [Fact]
    public async Task NoMessageId_NoSource_NoTarget_WithContent_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage);
        msg.SetStringContent("orphan content");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal("orphan content", read.ToString());
    }

    [Fact]
    public async Task NoContent_WithHeaders_RoundTrip()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("nch-001");
        msg.AddHeader("Key", "Value");

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);
        Assert.Equal("Value", read.FindHeader("Key"));
        Assert.Equal(0ul, read.Length);
    }

    #endregion

    #region Binary Content Integrity

    [Fact]
    public async Task BinaryContent_AllByteValues_PreservedExactly()
    {
        byte[] content = new byte[256];
        for (int i = 0; i < 256; i++)
            content[i] = (byte)i;

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("bin256");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.Equal(content, read.Content.ToArray());
    }

    [Fact]
    public async Task BinaryContent_AllZeros_Preserved()
    {
        byte[] content = new byte[500];

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("bin0");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.All(read.Content.ToArray(), b => Assert.Equal(0, b));
    }

    [Fact]
    public async Task BinaryContent_AllOnes_Preserved()
    {
        byte[] content = new byte[500];
        Array.Fill(content, (byte)0xFF);

        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId("binFF");
        msg.Content = new MemoryStream(content);
        msg.CalculateLengths();

        HorseMessage read = await WriteAndRead(msg);

        Assert.NotNull(read);
        Assert.All(read.Content.ToArray(), b => Assert.Equal(0xFF, b));
    }

    #endregion
}


