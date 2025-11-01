using System;
using System.Buffers;
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
    private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(false);

    /// <summary>
    /// Writes a Horse message to stream
    /// </summary>
    public static void Write(HorseMessage value, Stream stream, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        bool hasAdditionalHeader = additionalHeaders != null && additionalHeaders.Count > 0;

        using MemoryStream ms = new MemoryStream();
        WriteFrame(ms, value, hasAdditionalHeader);

        if (value.HasHeader || hasAdditionalHeader)
            WriteHeader(ms, value, additionalHeaders);

        if (value.Length > 0)
            WriteContent(ms, value);

        ms.WriteTo(stream);
    }

    /// <summary>
    /// Creates byte array of Horse message
    /// </summary>
    public static byte[] Create(HorseMessage value, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        bool hasAdditionalHeader = additionalHeaders != null && additionalHeaders.Count > 0;
        int estimatedSize = 256 + (int)(value.Content?.Length ?? 0);

        using MemoryStream ms = new MemoryStream(estimatedSize);
        WriteFrame(ms, value, hasAdditionalHeader);

        if (value.HasHeader || hasAdditionalHeader)
            WriteHeader(ms, value, additionalHeaders);

        if (value.Length > 0)
            WriteContent(ms, value);

        return ms.ToArray();
    }

    /// <summary>
    /// Writes frame to stream
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFrame(MemoryStream ms, HorseMessage message, bool hasAdditionalHeaders)
    {
        Span<byte> frameBuffer = stackalloc byte[256];
        int position = 0;

        byte proto = (byte)message.Type;

        if (message.WaitResponse)
            proto += 128;

        if (message.HighPriority)
            proto += 64;

        if (message.HasHeader || hasAdditionalHeaders)
            proto += 32;

        frameBuffer[position++] = proto;

        byte addContent = 0;
        if (message.HasAdditionalContent)
            addContent += 128;

        frameBuffer[position++] = addContent;
        frameBuffer[position++] = (byte)message.MessageIdLength;
        frameBuffer[position++] = (byte)message.SourceLength;
        frameBuffer[position++] = (byte)message.TargetLength;

        BitConverter.TryWriteBytes(frameBuffer.Slice(position, 2), message.ContentType);
        position += 2;

        if (message.Content != null && message.Length == 0)
            message.Length = (ulong)message.Content.Length;

        if (message.Length < 253)
        {
            frameBuffer[position++] = (byte)message.Length;
        }
        else if (message.Length <= ushort.MaxValue)
        {
            frameBuffer[position++] = 253;
            BitConverter.TryWriteBytes(frameBuffer.Slice(position, 2), (ushort)message.Length);
            position += 2;
        }
        else if (message.Length <= uint.MaxValue)
        {
            frameBuffer[position++] = 254;
            BitConverter.TryWriteBytes(frameBuffer.Slice(position, 4), (uint)message.Length);
            position += 4;
        }
        else
        {
            frameBuffer[position++] = 255;
            BitConverter.TryWriteBytes(frameBuffer.Slice(position, 8), message.Length);
            position += 8;
        }

        if (message.HasAdditionalContent)
        {
            BitConverter.TryWriteBytes(frameBuffer.Slice(position, 4), message.AdditionalContentLength);
            position += 4;
        }

        if (message.MessageIdLength > 0)
            position += _utf8NoBom.GetBytes(message.MessageId, frameBuffer.Slice(position));

        if (message.SourceLength > 0)
            position += _utf8NoBom.GetBytes(message.Source, frameBuffer.Slice(position));

        if (message.TargetLength > 0)
            position += _utf8NoBom.GetBytes(message.Target, frameBuffer.Slice(position));

        ms.Write(frameBuffer[..position]);

        /*
            byte proto = (byte)message.Type;

            if (message.WaitResponse)
                proto += 128;

            if (message.HighPriority)
                proto += 64;

            if (message.HasHeader || hasAdditionalHeaders)
                proto += 32;

            ms.WriteByte(proto);

            byte addContent = 0;
            if (message.HasAdditionalContent)
                addContent += 128;

            ms.WriteByte(addContent);

            ms.WriteByte((byte)message.MessageIdLength);
            ms.WriteByte((byte)message.SourceLength);
            ms.WriteByte((byte)message.TargetLength);

            ms.Write(BitConverter.GetBytes(message.ContentType));

            if (message.Content != null && message.Length == 0)
                message.Length = (ulong)message.Content.Length;

            if (message.Length < 253)
                ms.WriteByte((byte)message.Length);
            else if (message.Length <= ushort.MaxValue)
            {
                ms.WriteByte(253);
                ms.Write(BitConverter.GetBytes((ushort)message.Length));
            }
            else if (message.Length <= uint.MaxValue)
            {
                ms.WriteByte(254);
                ms.Write(BitConverter.GetBytes((uint)message.Length));
            }
            else
            {
                ms.WriteByte(255);
                ms.Write(BitConverter.GetBytes(message.Length));
            }

            if (message.HasAdditionalContent)
                ms.Write(BitConverter.GetBytes(message.AdditionalContentLength));

            if (message.MessageIdLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.MessageId);
                ms.Write(bytes);
            }

            if (message.SourceLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.Source);
                ms.Write(bytes);
            }

            if (message.TargetLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.Target);
                ms.Write(bytes);
            }

            */
    }

    /// <summary>
    /// Writes header length and content to stream
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(MemoryStream ms, HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders)
    {
        int estimatedSize = 0;
        if (message.HeadersList != null)
        {
            foreach (var pair in message.HeadersList)
                estimatedSize += Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value) + 3; // ':' + "\r\n"
        }

        if (additionalHeaders != null)
        {
            foreach (var pair in additionalHeaders)
                estimatedSize += Encoding.UTF8.GetByteCount(pair.Key) + Encoding.UTF8.GetByteCount(pair.Value) + 3;
        }

        byte[] headerBuffer = _arrayPool.Rent(estimatedSize);
        try
        {
            int position = 0;

            if (message.HeadersList != null)
            {
                foreach (KeyValuePair<string, string> pair in message.HeadersList)
                {
                    position += _utf8NoBom.GetBytes(pair.Key, 0, pair.Key.Length, headerBuffer, position);
                    headerBuffer[position++] = (byte)':';
                    position += _utf8NoBom.GetBytes(pair.Value, 0, pair.Value.Length, headerBuffer, position);
                    headerBuffer[position++] = (byte)'\r';
                    headerBuffer[position++] = (byte)'\n';
                }
            }

            if (additionalHeaders != null)
            {
                foreach (KeyValuePair<string, string> pair in additionalHeaders)
                {
                    position += _utf8NoBom.GetBytes(pair.Key, 0, pair.Key.Length, headerBuffer, position);
                    headerBuffer[position++] = (byte)':';
                    position += _utf8NoBom.GetBytes(pair.Value, 0, pair.Value.Length, headerBuffer, position);
                    headerBuffer[position++] = (byte)'\r';
                    headerBuffer[position++] = (byte)'\n';
                }
            }

            Span<byte> lengthBytes = stackalloc byte[2];
            BitConverter.TryWriteBytes(lengthBytes, (ushort)position);
            ms.Write(lengthBytes);

            ms.Write(headerBuffer, 0, position);
        }
        finally
        {
            _arrayPool.Return(headerBuffer);
        }

        /*
        using MemoryStream headerStream = new MemoryStream();

        if (message.HeadersList != null)
            foreach (KeyValuePair<string, string> pair in message.HeadersList)
                headerStream.Write(Encoding.UTF8.GetBytes(pair.Key + ":" + pair.Value + "\r\n"));

        if (additionalHeaders != null)
            foreach (KeyValuePair<string, string> pair in additionalHeaders)
                headerStream.Write(Encoding.UTF8.GetBytes(pair.Key + ":" + pair.Value + "\r\n"));

        ms.Write(BitConverter.GetBytes((ushort) headerStream.Length));
        headerStream.Position = 0;
        headerStream.WriteTo(ms);*/
    }

    /// <summary>
    /// Writes content to stream
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteContent(MemoryStream ms, HorseMessage message)
    {
        if (message.Length > 0 && message.Content != null)
            message.Content.WriteTo(ms);

        if (message.HasAdditionalContent && message.AdditionalContent != null)
            message.AdditionalContent.WriteTo(ms);

        ms.Position = 0;
    }
}