using System.Text;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

/// <summary>
/// Tests for HorseMessage operations: headers, content, clone, dispose, create helpers.
/// </summary>
public class HorseMessageTest
{
    #region Header Operations

    [Fact]
    public void FindHeader_ReturnsValue()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key", "Value");

        Assert.Equal("Value", msg.FindHeader("Key"));
    }

    [Fact]
    public void FindHeader_CaseInsensitive()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Content-Type", "text/plain");

        Assert.Equal("text/plain", msg.FindHeader("content-type"));
        Assert.Equal("text/plain", msg.FindHeader("CONTENT-TYPE"));
    }

    [Fact]
    public void FindHeader_ReturnsNull_WhenNotFound()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key", "Value");

        Assert.Null(msg.FindHeader("NonExistent"));
    }

    [Fact]
    public void FindHeader_ReturnsNull_WhenNoHeaders()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");

        Assert.Null(msg.FindHeader("Key"));
    }

    [Fact]
    public void SetOrAddHeader_SetsNewHeader()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetOrAddHeader("Key", "Value");

        Assert.Equal("Value", msg.FindHeader("Key"));
        Assert.True(msg.HasHeader);
    }

    [Fact]
    public void SetOrAddHeader_UpdatesExistingHeader()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key", "OldValue");
        msg.SetOrAddHeader("Key", "NewValue");

        Assert.Equal("NewValue", msg.FindHeader("Key"));
    }

    [Fact]
    public void SetOrAddHeader_CaseInsensitiveMatch()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Content-Type", "text/plain");
        msg.SetOrAddHeader("content-type", "application/json");

        // Should have only one header
        Assert.Single(msg.Headers);
        Assert.Equal("application/json", msg.FindHeader("Content-Type"));
    }

    [Fact]
    public void RemoveHeader_ByKey()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key1", "Value1");
        msg.AddHeader("Key2", "Value2");

        msg.RemoveHeader("Key1");

        Assert.Null(msg.FindHeader("Key1"));
        Assert.Equal("Value2", msg.FindHeader("Key2"));
    }

    [Fact]
    public void RemoveHeader_CaseInsensitive()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Content-Type", "text/plain");

        msg.RemoveHeader("content-type");

        Assert.Null(msg.FindHeader("Content-Type"));
        Assert.False(msg.HasHeader);
    }

    [Fact]
    public void RemoveHeaders_MultipleKeys()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key1", "Value1");
        msg.AddHeader("Key2", "Value2");
        msg.AddHeader("Key3", "Value3");

        msg.RemoveHeaders("Key1", "Key3");

        Assert.Null(msg.FindHeader("Key1"));
        Assert.Equal("Value2", msg.FindHeader("Key2"));
        Assert.Null(msg.FindHeader("Key3"));
    }

    [Fact]
    public void AddHeader_IntValue()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Count", 42);

        Assert.Equal("42", msg.FindHeader("Count"));
    }

    [Fact]
    public void AddHeader_UShortValue()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Code", (ushort)200);

        Assert.Equal("200", msg.FindHeader("Code"));
    }

    #endregion

    #region Content Operations

    [Fact]
    public void SetStringContent_SetsContentAndLength()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("Hello World");

        Assert.NotNull(msg.Content);
        Assert.Equal((ulong)Encoding.UTF8.GetByteCount("Hello World"), msg.Length);
    }

    [Fact]
    public void SetStringContent_NullOrEmpty_DoesNothing()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent(null);
        Assert.Null(msg.Content);

        msg.SetStringContent("");
        Assert.Null(msg.Content);
    }

    [Fact]
    public void ToString_ReturnsContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("test content");

        Assert.Equal("test content", msg.ToString());
    }

    [Fact]
    public void ToString_ReturnsEmpty_WhenNoContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");

        Assert.Equal(string.Empty, msg.ToString());
    }

    [Fact]
    public void GetStringContent_ReturnsContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("get string test");

        Assert.Equal("get string test", msg.GetStringContent());
    }

    [Fact]
    public void GetStringContent_ReturnsNull_WhenNoContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");

        Assert.Null(msg.GetStringContent());
    }

    [Fact]
    public void SetStringContent_UnicodeContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("Merhaba Dünya! 🐎 ÇŞĞÜÖİ");

        Assert.Equal("Merhaba Dünya! 🐎 ÇŞĞÜÖİ", msg.ToString());
    }

    [Fact]
    public void SetStringAdditionalContent_Sets()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringAdditionalContent("extra data");

        Assert.NotNull(msg.AdditionalContent);
        Assert.True(msg.HasAdditionalContent);
        Assert.True(msg.AdditionalContentLength > 0);
    }

    #endregion

    #region Clone

    [Fact]
    public void Clone_WithHeaders()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("orig-001");
        msg.SetSource("source");
        msg.AddHeader("H1", "V1");
        msg.SetStringContent("content");
        msg.HighPriority = true;
        msg.WaitResponse = true;

        HorseMessage clone = msg.Clone(true, true, "clone-001");

        Assert.Equal("clone-001", clone.MessageId);
        Assert.Equal("source", clone.Source);
        Assert.Equal("queue", clone.Target);
        Assert.Equal(msg.ContentType, clone.ContentType);
        Assert.True(clone.HighPriority);
        Assert.True(clone.WaitResponse);
        Assert.True(clone.HasHeader);
        Assert.Equal("V1", clone.FindHeader("H1"));
        Assert.Equal("content", clone.ToString());
    }

    [Fact]
    public void Clone_WithoutHeaders()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("orig-001");
        msg.AddHeader("H1", "V1");
        msg.SetStringContent("content");

        HorseMessage clone = msg.Clone(false, true, "clone-002");

        Assert.Equal("clone-002", clone.MessageId);
        Assert.False(clone.HasHeader);
        Assert.Equal("content", clone.ToString());
    }

    [Fact]
    public void Clone_WithoutContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("orig-001");
        msg.SetStringContent("content");

        HorseMessage clone = msg.Clone(false, false, "clone-003");

        Assert.Equal("clone-003", clone.MessageId);
        Assert.Null(clone.Content);
    }

    [Fact]
    public void Clone_WithAdditionalHeaders()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("orig-001");

        List<KeyValuePair<string, string>> extra = [new("Extra", "Header")];
        HorseMessage clone = msg.Clone(false, false, "clone-004", extra);

        Assert.True(clone.HasHeader);
        Assert.Equal("Header", clone.FindHeader("Extra"));
    }

    [Fact]
    public void Clone_ContentIsIndependent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("original");

        HorseMessage clone = msg.Clone(false, true, "clone-005");

        // Modify clone content
        clone.SetStringContent("modified");

        // Original should be unchanged
        Assert.Equal("original", msg.ToString());
        Assert.Equal("modified", clone.ToString());
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_DisposesStreams()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("content");
        msg.SetStringAdditionalContent("extra");

        MemoryStream content = msg.Content;
        MemoryStream additional = msg.AdditionalContent;

        msg.Dispose();

        // After dispose, writing to the streams should throw
        Assert.Throws<ObjectDisposedException>(() => content.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => additional.WriteByte(0));
    }

    [Fact]
    public void Dispose_NullStreams_DoesNotThrow()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");

        // Should not throw even with null streams
        msg.Dispose();
    }

    [Fact]
    public void Dispose_DoubleDispose_DoesNotThrow()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetStringContent("content");

        msg.Dispose();
        msg.Dispose(); // double dispose should be safe
    }

    #endregion

    #region CalculateLengths

    [Fact]
    public void CalculateLengths_SetsContentLength()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetMessageId("msg-id");
        msg.SetSource("source");
        msg.SetTarget("target");
        msg.Content = new MemoryStream(new byte[100]);

        msg.CalculateLengths();

        Assert.Equal(100ul, msg.Length);
    }

    [Fact]
    public void CalculateLengths_NullValues_ZeroLength()
    {
        HorseMessage msg = new HorseMessage();

        msg.CalculateLengths();

        Assert.Equal(0ul, msg.Length);
    }

    #endregion

    #region CreateAcknowledge / CreateResponse

    [Fact]
    public void CreateAcknowledge_FromDirectMessage()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetMessageId("ack-001");
        msg.SetSource("source");

        HorseMessage ack = msg.CreateAcknowledge();

        Assert.Equal(MessageType.Response, ack.Type);
        Assert.Equal("ack-001", ack.MessageId);
        Assert.True(ack.HighPriority); // Direct messages have high priority ack
        Assert.Equal("target", ack.Source);
        Assert.Equal("source", ack.Target);
    }

    [Fact]
    public void CreateAcknowledge_FromQueueMessage()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-name");
        msg.SetMessageId("ack-002");

        HorseMessage ack = msg.CreateAcknowledge();

        Assert.Equal(MessageType.Response, ack.Type);
        Assert.Equal("ack-002", ack.MessageId);
        Assert.False(ack.HighPriority);
        Assert.Equal("queue-name", ack.Target);
    }

    [Fact]
    public void CreateAcknowledge_WithNegativeReason()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue-name");
        msg.SetMessageId("ack-003");

        HorseMessage ack = msg.CreateAcknowledge("processing-failed");

        Assert.Equal(MessageType.Response, ack.Type);
        Assert.Equal(KnownContentTypes.Failed, ack.ContentType);
        Assert.True(ack.HasHeader);
        Assert.Equal("processing-failed", ack.FindHeader(HorseHeaders.NEGATIVE_ACKNOWLEDGE_REASON));
    }

    [Fact]
    public void CreateResponse_WithStatus()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetMessageId("resp-001");
        msg.SetSource("source");

        HorseMessage response = msg.CreateResponse(HorseResultCode.Ok);

        Assert.Equal(MessageType.Response, response.Type);
        Assert.Equal("resp-001", response.MessageId);
        Assert.Equal(Convert.ToUInt16(HorseResultCode.Ok), response.ContentType);
    }

    #endregion
}


