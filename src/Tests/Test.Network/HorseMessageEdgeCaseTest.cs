using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Xunit;

namespace Test.Network;

/// <summary>
/// Additional edge case tests for HorseMessage that fill coverage gaps:
/// constructors, setter edge cases, RemoveHeader(KVP), CreateResponse variants.
/// </summary>
public class HorseMessageEdgeCaseTest
{
    #region Constructors

    [Fact]
    public void DefaultConstructor_AllDefaults()
    {
        HorseMessage msg = new HorseMessage();

        Assert.Equal(MessageType.Other, msg.Type);
        Assert.False(msg.WaitResponse);
        Assert.False(msg.HighPriority);
        Assert.False(msg.HasHeader);
        Assert.False(msg.HasAdditionalContent);
        Assert.Null(msg.MessageId);
        Assert.Null(msg.Target);
        Assert.Null(msg.Source);
        Assert.Equal(0ul, msg.Length);
        Assert.Equal(0, msg.ContentType);
        Assert.Null(msg.Content);
        Assert.Null(msg.AdditionalContent);
        Assert.Null(msg.Headers);
    }

    [Fact]
    public void TypeConstructor_SetsType()
    {
        HorseMessage msg = new HorseMessage(MessageType.Channel);
        Assert.Equal(MessageType.Channel, msg.Type);
    }

    [Fact]
    public void TypeTargetConstructor_SetsBoth()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "my-queue");
        Assert.Equal(MessageType.QueueMessage, msg.Type);
        Assert.Equal("my-queue", msg.Target);
    }

    [Fact]
    public void TypeTargetContentTypeConstructor_SetsAll()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target", 999);
        Assert.Equal(MessageType.Server, msg.Type);
        Assert.Equal("target", msg.Target);
        Assert.Equal(999, msg.ContentType);
    }

    #endregion

    #region SetMessageId Edge Cases

    [Fact]
    public void SetMessageId_Null_SetsZeroLength()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetMessageId("initial");
        msg.SetMessageId(null);

        Assert.Null(msg.MessageId);
    }

    [Fact]
    public void SetMessageId_Empty_SetsZeroLength()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetMessageId("");

        Assert.Equal("", msg.MessageId);
    }

    [Fact]
    public void SetMessageId_Unicode()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetMessageId("mesaj-ÇŞĞ-001");

        Assert.Equal("mesaj-ÇŞĞ-001", msg.MessageId);
    }

    #endregion

    #region SetSource / SetTarget Edge Cases

    [Fact]
    public void SetSource_Null()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetSource("source");
        msg.SetSource(null);

        Assert.Null(msg.Source);
    }

    [Fact]
    public void SetTarget_Null()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetTarget("target");
        msg.SetTarget(null);

        Assert.Null(msg.Target);
    }

    [Fact]
    public void SetSource_Empty()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetSource("");

        Assert.Equal("", msg.Source);
    }

    [Fact]
    public void SetTarget_Empty()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetTarget("");

        Assert.Equal("", msg.Target);
    }

    #endregion

    #region RemoveHeader(KeyValuePair) Overload

    [Fact]
    public void RemoveHeader_ByKeyValuePair()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key1", "Value1");
        msg.AddHeader("Key2", "Value2");

        KeyValuePair<string, string> toRemove = new("Key1", "Value1");
        msg.RemoveHeader(toRemove);

        Assert.Null(msg.FindHeader("Key1"));
        Assert.Equal("Value2", msg.FindHeader("Key2"));
    }

    [Fact]
    public void RemoveHeader_ByKVP_RemovesLastHeader_SetsFalse()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Only", "Header");

        KeyValuePair<string, string> toRemove = new("Only", "Header");
        msg.RemoveHeader(toRemove);

        Assert.False(msg.HasHeader);
    }

    #endregion

    #region RemoveHeaders Edge Cases

    [Fact]
    public void RemoveHeaders_NullHeadersList_DoesNotThrow()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        // HeadersList is null, should not throw
        msg.RemoveHeaders("Key1", "Key2");
    }

    [Fact]
    public void RemoveHeaders_EmptyKeys_DoesNothing()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.AddHeader("Key1", "Value1");

        msg.RemoveHeaders();

        Assert.Equal("Value1", msg.FindHeader("Key1"));
    }

    #endregion

    #region SetOrAddHeader Edge Cases

    [Fact]
    public void SetOrAddHeader_WhenHeadersListNull_CreatesNew()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetOrAddHeader("NewKey", "NewValue");

        Assert.True(msg.HasHeader);
        Assert.Equal("NewValue", msg.FindHeader("NewKey"));
    }

    [Fact]
    public void SetOrAddHeader_Multiple_Distinct()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetOrAddHeader("Key1", "Val1");
        msg.SetOrAddHeader("Key2", "Val2");
        msg.SetOrAddHeader("Key3", "Val3");

        Assert.Equal("Val1", msg.FindHeader("Key1"));
        Assert.Equal("Val2", msg.FindHeader("Key2"));
        Assert.Equal("Val3", msg.FindHeader("Key3"));
    }

    #endregion

    #region CreateResponse Variants

    [Fact]
    public void CreateResponse_NotFound()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target");
        msg.SetMessageId("r-404");
        msg.SetSource("source");

        HorseMessage resp = msg.CreateResponse(HorseResultCode.NotFound);

        Assert.Equal(MessageType.Response, resp.Type);
        Assert.Equal(Convert.ToUInt16(HorseResultCode.NotFound), resp.ContentType);
        Assert.Equal("r-404", resp.MessageId);
        Assert.True(resp.HighPriority); // DirectMessage -> high priority
    }

    [Fact]
    public void CreateResponse_Unauthorized()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("r-401");
        msg.SetSource("source");

        HorseMessage resp = msg.CreateResponse(HorseResultCode.Unauthorized);

        Assert.Equal(MessageType.Response, resp.Type);
        Assert.Equal(Convert.ToUInt16(HorseResultCode.Unauthorized), resp.ContentType);
        Assert.False(resp.HighPriority); // QueueMessage -> not high priority
        Assert.Equal("queue", resp.Target); // queue messages target = queue name
    }

    [Fact]
    public void CreateResponse_FromQueueMessage_TargetIsQueue()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "my-queue");
        msg.SetMessageId("r-q");
        msg.SetSource("client-1");

        HorseMessage resp = msg.CreateResponse(HorseResultCode.Ok);

        Assert.Equal("my-queue", resp.Target);
    }

    [Fact]
    public void CreateResponse_FromDirectMessage_TargetIsSource()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "target-client");
        msg.SetMessageId("r-d");
        msg.SetSource("source-client");

        HorseMessage resp = msg.CreateResponse(HorseResultCode.Ok);

        Assert.Equal("source-client", resp.Target);
    }

    #endregion

    #region CreateAcknowledge Edge Cases

    [Fact]
    public void CreateAcknowledge_NullReason_PositiveAck()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("ack-pos");

        HorseMessage ack = msg.CreateAcknowledge(null);

        Assert.Equal(Convert.ToUInt16(HorseResultCode.Ok), ack.ContentType);
    }

    [Fact]
    public void CreateAcknowledge_EmptyReason_PositiveAck()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("ack-empty");

        HorseMessage ack = msg.CreateAcknowledge("");

        Assert.Equal(Convert.ToUInt16(HorseResultCode.Ok), ack.ContentType);
    }

    [Fact]
    public void CreateAcknowledge_DirectMessage_SourceAndTargetSwapped()
    {
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, "receiver-id");
        msg.SetMessageId("ack-swap");
        msg.SetSource("sender-id");

        HorseMessage ack = msg.CreateAcknowledge();

        Assert.Equal("receiver-id", ack.Source);
        Assert.Equal("sender-id", ack.Target);
        Assert.True(ack.HighPriority);
    }

    #endregion

    #region Content Operations Edge Cases

    [Fact]
    public void ToString_ContentWithPosition()
    {
        // Content stream with position not at 0
        HorseMessage msg = new HorseMessage();
        byte[] bytes = Encoding.UTF8.GetBytes("Hello World");
        msg.Content = new MemoryStream(bytes);
        msg.Content.Position = 5; // mid-stream
        msg.CalculateLengths();

        // ToString should still return full content via ToArray
        string result = msg.ToString();
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void GetStringContent_ReturnsNull_WhenLengthZero()
    {
        HorseMessage msg = new HorseMessage();
        msg.Content = new MemoryStream([]);

        // Length is 0
        Assert.Null(msg.GetStringContent());
    }

    [Fact]
    public void SetStringAdditionalContent_Null_DoesNothing()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetStringAdditionalContent(null);

        Assert.Null(msg.AdditionalContent);
        Assert.False(msg.HasAdditionalContent);
    }

    [Fact]
    public void SetStringAdditionalContent_Empty_DoesNothing()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetStringAdditionalContent("");

        Assert.Null(msg.AdditionalContent);
        Assert.False(msg.HasAdditionalContent);
    }

    #endregion

    #region CalculateLengths with AdditionalContent

    [Fact]
    public void CalculateLengths_WithContentAndAdditional()
    {
        HorseMessage msg = new HorseMessage();
        msg.SetMessageId("calc");
        msg.SetSource("src");
        msg.SetTarget("tgt");
        msg.Content = new MemoryStream(new byte[200]);
        msg.SetStringAdditionalContent("additional data here");

        msg.CalculateLengths();

        Assert.Equal(200ul, msg.Length);
    }

    [Fact]
    public void CalculateLengths_ResetsPreviousLength()
    {
        HorseMessage msg = new HorseMessage();
        msg.Content = new MemoryStream(new byte[100]);
        msg.CalculateLengths();
        Assert.Equal(100ul, msg.Length);

        // Remove content
        msg.Content = null;
        msg.CalculateLengths();
        Assert.Equal(0ul, msg.Length);
    }

    #endregion

    #region Clone Edge Cases

    [Fact]
    public void Clone_WithAdditionalContent()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("clone-ac");
        msg.SetStringContent("main");
        msg.SetStringAdditionalContent("additional");

        HorseMessage clone = msg.Clone(false, true, "clone-ac-new");

        Assert.NotNull(clone.Content);
        Assert.NotNull(clone.AdditionalContent);
        Assert.True(clone.HasAdditionalContent);
        Assert.True(clone.AdditionalContentLength > 0);
    }

    [Fact]
    public void Clone_NullId_PreservesNullId()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("orig");

        HorseMessage clone = msg.Clone(false, false, null);

        Assert.Null(clone.MessageId);
    }

    [Fact]
    public void Clone_EmptyContent_NoContentInClone()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        // Content is null

        HorseMessage clone = msg.Clone(false, true, "clone-empty");

        Assert.Null(clone.Content);
    }

    [Fact]
    public void Clone_AdditionalHeadersMergedWithOriginal()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.AddHeader("Original", "Value1");

        List<KeyValuePair<string, string>> extra = [new("Extra", "Value2")];
        HorseMessage clone = msg.Clone(true, false, "merge-test", extra);

        Assert.Equal("Value1", clone.FindHeader("Original"));
        Assert.Equal("Value2", clone.FindHeader("Extra"));
    }

    #endregion

    #region Protocol Round-trip with Edge Cases

    [Fact]
    public async Task RoundTrip_NoMessageId_NoSourceNoTarget()
    {
        HorseMessage msg = new HorseMessage(MessageType.Ping);

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.Equal(MessageType.Ping, read.Type);
        Assert.Null(read.MessageId);
    }

    [Fact]
    public async Task RoundTrip_ZeroContentType()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target", 0);
        msg.SetMessageId("ct-zero");

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.Equal(0, read.ContentType);
    }

    [Fact]
    public async Task RoundTrip_LongMessageId()
    {
        // MessageId is encoded as octet-length (1 byte = max 255 bytes).
        // frameBuffer is 256 bytes and also holds source/target, so keep it reasonable.
        string longId = new string('M', 100);
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "q");
        msg.SetMessageId(longId);

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.Equal(longId, read.MessageId);
    }

    [Fact]
    public async Task RoundTrip_LongSourceAndTarget()
    {
        // Source + Target + MessageId combined must fit in 256-byte frame buffer
        string longSrc = new string('S', 80);
        string longTgt = new string('T', 80);
        HorseMessage msg = new HorseMessage(MessageType.DirectMessage, longTgt);
        msg.SetMessageId("long-st");
        msg.SetSource(longSrc);

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.Equal(longSrc, read.Source);
        Assert.Equal(longTgt, read.Target);
    }

    [Fact]
    public async Task RoundTrip_ManyHeaders()
    {
        HorseMessage msg = new HorseMessage(MessageType.Server, "target");
        msg.SetMessageId("many-h");

        for (int i = 0; i < 50; i++)
            msg.AddHeader($"Header-{i}", $"Value-{i}");

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.True(read.HasHeader);

        for (int i = 0; i < 50; i++)
            Assert.Equal($"Value-{i}", read.FindHeader($"Header-{i}"));
    }

    [Fact]
    public async Task RoundTrip_AdditionalContent_RequiresMainContent()
    {
        // Protocol design: additional content is appended after main content.
        // When main content length is 0, the reader skips to next message boundary.
        // So additional content ONLY works alongside main content.
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("ac-req");
        msg.SetStringContent("main required"); // main content is required
        msg.SetStringAdditionalContent("additional here");
        msg.CalculateLengths();

        byte[] data = HorseProtocolWriter.Create(msg);
        using MemoryStream ms = new MemoryStream(data);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(ms);

        Assert.NotNull(read);
        Assert.True(read.HasAdditionalContent);
        Assert.NotNull(read.AdditionalContent);
        Assert.Equal("main required", read.ToString());

        string additional = Encoding.UTF8.GetString(read.AdditionalContent.ToArray());
        Assert.Equal("additional here", additional);
    }

    #endregion

    #region ProtocolWriter Non-Seekable Stream

    [Fact]
    public async Task Writer_NonSeekableStream_FallsBackToCreate()
    {
        HorseMessage msg = new HorseMessage(MessageType.QueueMessage, "queue");
        msg.SetMessageId("ns-001");
        msg.AddHeader("Key", "Value");
        msg.SetStringContent("non-seekable content");

        // Write to a non-seekable wrapper stream
        using NonSeekableStream ns = new NonSeekableStream();
        HorseProtocolWriter.Write(msg, ns);

        byte[] written = ns.ToArray();
        Assert.True(written.Length > 0);

        // Verify the output is readable
        using MemoryStream readMs = new MemoryStream(written);
        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage read = await reader.Read(readMs);

        Assert.NotNull(read);
        Assert.Equal("ns-001", read.MessageId);
        Assert.Equal("Value", read.FindHeader("Key"));
        Assert.Equal("non-seekable content", read.ToString());
    }

    #endregion
}

/// <summary>
/// A stream wrapper that reports CanSeek=false to test the non-seekable path in HorseProtocolWriter.
/// </summary>
internal class NonSeekableStream : Stream
{
    private readonly MemoryStream _inner = new();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
        => _inner.Write(buffer, offset, count);

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => _inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public byte[] ToArray() => _inner.ToArray();
}

