using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Horse Message writer
/// </summary>
public class HorseProtocolWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Writes a Horse message to stream
    /// </summary>
    public static void Write(HorseMessage value, Stream stream, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            byte[] data = Create(value, additionalHeaders);
            stream.Write(data, 0, data.Length);
            return;
        }

        PrepareFrame(value);
        bool hasHeader = value.HasHeader || additionalHeaders is { Count: > 0 };
        int headerLength = hasHeader ? GetHeaderLength(value, additionalHeaders) : 0;

        // Each of the three identifiers has a one-byte length, in addition to
        // the fixed frame and the optional length fields.
        Span<byte> frame = stackalloc byte[8 + 8 + 4 + 3 * byte.MaxValue];
        int frameLength = WriteFrame(frame, value, hasHeader);
        if (frameLength != GetFrameLength(value))
            throw new InvalidOperationException("Message identifiers changed during serialization.");
        stream.Write(frame[..frameLength]);

        if (hasHeader)
            WriteHeader(stream, value, additionalHeaders, headerLength);

        if (value.Length > 0 || value.HasAdditionalContent)
        {
            WriteContent(stream, value);
            stream.Position = 0;
        }
    }

    /// <summary>
    /// Creates a Horse message byte array. Standard MemoryStream payloads use
    /// one payload-sized allocation. Derived streams retain virtual WriteTo
    /// behavior through a compatibility fallback.
    /// </summary>
    public static byte[] Create(HorseMessage value, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        PrepareFrame(value);

        // MemoryStream.WriteTo is virtual. Custom streams can transform or
        // expand the emitted bytes, so their output cannot be sized from the
        // backing buffer. Keep virtual dispatch for those uncommon streams.
        if ((value.Content != null && value.Content.GetType() != typeof(MemoryStream)) ||
            (value.HasAdditionalContent && value.AdditionalContent != null && value.AdditionalContent.GetType() != typeof(MemoryStream)))
        {
            using MemoryStream output = new();
            Write(value, output, additionalHeaders);
            return output.ToArray();
        }

        bool hasHeader = value.HasHeader || additionalHeaders is { Count: > 0 };
        int headerLength = hasHeader ? GetHeaderLength(value, additionalHeaders) : 0;
        int frameLength = GetFrameLength(value);
        long contentLength = value.Length > 0 && value.Content != null ? value.Content.Length : 0;
        long additionalLength = value.HasAdditionalContent && value.AdditionalContent != null ? value.AdditionalContent.Length : 0;
        int size = checked((int)(frameLength + (hasHeader ? 2 + headerLength : 0) + contentLength + additionalLength));

        byte[] data = new byte[size];
        int position = WriteFrame(data, value, hasHeader);
        if (position != frameLength)
            throw new InvalidOperationException("Message identifiers changed during serialization.");
        if (hasHeader)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(position, 2), (ushort)headerLength);
            position += 2;
            int written = WriteHeaders(data.AsSpan(position, headerLength), value, additionalHeaders);
            if (written != headerLength)
                throw new InvalidOperationException("Message headers changed during serialization.");
            position += written;
        }

        if (value.Length > 0 && value.Content != null)
        {
            value.Content.Position = 0;
            if (contentLength > 0)
                position += CopyContent(value.Content, data, position);
        }

        if (additionalLength > 0)
            CopyContent(value.AdditionalContent, data, position);

        return data;
    }

    private static void PrepareFrame(HorseMessage message)
    {
        if ((uint)message.MessageIdLength > byte.MaxValue ||
            (uint)message.SourceLength > byte.MaxValue ||
            (uint)message.TargetLength > byte.MaxValue)
            throw new ArgumentException("Message id, source and target must each fit in 255 UTF-8 bytes.", nameof(message));

        if (message.Content != null && message.Length == 0)
            message.Length = (ulong)message.Content.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFrameLength(HorseMessage message)
    {
        int lengthBytes = message.Length < 253 ? 0 : message.Length <= ushort.MaxValue ? 2 : message.Length <= uint.MaxValue ? 4 : 8;
        return 8 + lengthBytes + (message.HasAdditionalContent ? 4 : 0) + message.MessageIdLength + message.SourceLength + message.TargetLength;
    }

    /// <summary>
    /// Writes frame to the destination span.
    /// </summary>
    private static int WriteFrame(Span<byte> destination, HorseMessage message, bool hasHeader)
    {
        int position = 0;
        byte proto = (byte)message.Type;
        if (message.WaitResponse)
            proto += 128;
        if (message.HighPriority)
            proto += 64;
        if (hasHeader)
            proto += 32;

        destination[position++] = proto;
        destination[position++] = message.HasAdditionalContent ? (byte)128 : (byte)0;
        destination[position++] = (byte)message.MessageIdLength;
        destination[position++] = (byte)message.SourceLength;
        destination[position++] = (byte)message.TargetLength;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(position, 2), message.ContentType);
        position += 2;

        if (message.Length < 253)
            destination[position++] = (byte)message.Length;
        else if (message.Length <= ushort.MaxValue)
        {
            destination[position++] = 253;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(position, 2), (ushort)message.Length);
            position += 2;
        }
        else if (message.Length <= uint.MaxValue)
        {
            destination[position++] = 254;
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(position, 4), (uint)message.Length);
            position += 4;
        }
        else
        {
            destination[position++] = 255;
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(position, 8), message.Length);
            position += 8;
        }

        if (message.HasAdditionalContent)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(position, 4), message.AdditionalContentLength);
            position += 4;
        }

        if (message.MessageIdLength > 0)
            position += Utf8NoBom.GetBytes(message.MessageId, destination[position..]);
        if (message.SourceLength > 0)
            position += Utf8NoBom.GetBytes(message.Source, destination[position..]);
        if (message.TargetLength > 0)
            position += Utf8NoBom.GetBytes(message.Target, destination[position..]);

        return position;
    }

    private static int GetHeaderLength(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders)
    {
        int length = 0;
        if (message.HeadersList != null)
            for (int i = 0; i < message.HeadersList.Count; i++)
                length = checked(length + GetHeaderLength(message.HeadersList[i]));

        if (additionalHeaders != null)
            for (int i = 0; i < additionalHeaders.Count; i++)
                length = checked(length + GetHeaderLength(additionalHeaders[i]));

        if (length > ushort.MaxValue)
            throw new ArgumentException("Message headers must fit in 65535 UTF-8 bytes.", nameof(message));

        return length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetHeaderLength(KeyValuePair<string, string> header)
    {
        return checked(Utf8NoBom.GetByteCount(header.Key) + Utf8NoBom.GetByteCount(header.Value) + 3);
    }

    private static int WriteHeaders(Span<byte> destination, HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders)
    {
        int position = 0;
        if (message.HeadersList != null)
            for (int i = 0; i < message.HeadersList.Count; i++)
                position += WriteHeader(destination[position..], message.HeadersList[i]);

        if (additionalHeaders != null)
            for (int i = 0; i < additionalHeaders.Count; i++)
                position += WriteHeader(destination[position..], additionalHeaders[i]);

        return position;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteHeader(Span<byte> destination, KeyValuePair<string, string> header)
    {
        int position = Utf8NoBom.GetBytes(header.Key, destination);
        destination[position++] = (byte)':';
        position += Utf8NoBom.GetBytes(header.Value, destination[position..]);
        destination[position++] = (byte)'\r';
        destination[position++] = (byte)'\n';
        return position;
    }

    private static void WriteHeader(Stream stream, HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders, int headerLength)
    {
        byte[] rented = null;
        int size = headerLength + 2;
        Span<byte> header = size <= 1024 ? stackalloc byte[size] : (rented = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);
        try
        {
            BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)headerLength);
            int written = WriteHeaders(header[2..], message, additionalHeaders);
            if (written != headerLength)
                throw new InvalidOperationException("Message headers changed during serialization.");
            stream.Write(header);
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int CopyContent(MemoryStream content, byte[] destination, int offset)
    {
        int length = checked((int)content.Length);
        if (content.TryGetBuffer(out ArraySegment<byte> buffer))
            buffer.AsSpan().CopyTo(destination.AsSpan(offset, length));
        else
        {
            // WriteTo also supports non-exposable streams without ToArray and
            // does not depend on the shared source stream's Position.
            using MemoryStream target = new MemoryStream(destination, offset, length, true, true);
            content.WriteTo(target);
        }

        return length;
    }

    private static void WriteContent(Stream stream, HorseMessage message)
    {
        if (message.Length > 0 && message.Content != null)
        {
            message.Content.Position = 0;
            message.Content.WriteTo(stream);
        }
        if (message.HasAdditionalContent && message.AdditionalContent != null)
            message.AdditionalContent.WriteTo(stream);
    }
}
