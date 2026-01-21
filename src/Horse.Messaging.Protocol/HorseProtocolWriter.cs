using System;
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
        bool hasAdditionalHeader = additionalHeaders != null && additionalHeaders.Count > 0;

        WriteFrame(stream, value, hasAdditionalHeader);

        if (value.HasHeader || hasAdditionalHeader)
            WriteHeader(stream, value, additionalHeaders);

        if (value.Length > 0)
            WriteContent(stream, value);
    }

    /// <summary>
    /// Creates byte array of Horse message
    /// </summary>
    public static byte[] Create(HorseMessage value, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        bool hasAdditionalHeader = additionalHeaders != null && additionalHeaders.Count > 0;

        using MemoryStream ms = new MemoryStream();
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
    private static void WriteFrame(Stream ms, HorseMessage message, bool hasAdditionalHeaders)
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
            position += Utf8NoBom.GetBytes(message.MessageId, frameBuffer.Slice(position));

        if (message.SourceLength > 0)
            position += Utf8NoBom.GetBytes(message.Source, frameBuffer.Slice(position));

        if (message.TargetLength > 0)
            position += Utf8NoBom.GetBytes(message.Target, frameBuffer.Slice(position));

        ms.Write(frameBuffer[..position]);
    }

    /// <summary>
    /// Writes header length and content to stream
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHeader(Stream ms, HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders)
    {
        long startPosition = ms.Position;
        ms.Write("\0\0"u8);

        Span<byte> newLine = stackalloc byte[2];
        newLine[0] = (byte)'\r';
        newLine[1] = (byte)'\n';

        if (message.HeadersList != null)
        {
            foreach (KeyValuePair<string, string> pair in message.HeadersList)
            {
                ms.Write(Utf8NoBom.GetBytes(pair.Key));
                ms.WriteByte((byte)':');
                ms.Write(Utf8NoBom.GetBytes(pair.Value));
                ms.Write(newLine);
            }
        }

        if (additionalHeaders != null)
        {
            foreach (KeyValuePair<string, string> pair in additionalHeaders)
            {
                ms.Write(Utf8NoBom.GetBytes(pair.Key));
                ms.WriteByte((byte)':');
                ms.Write(Utf8NoBom.GetBytes(pair.Value));
                ms.Write(newLine);
            }
        }

        long endingPosition = ms.Position;

        ushort headerSize = (ushort)(ms.Position - startPosition - 2);
        Span<byte> lengthBytes = stackalloc byte[2];
        BitConverter.TryWriteBytes(lengthBytes, headerSize);

        ms.Position = startPosition;
        ms.Write(lengthBytes);
        ms.Position = endingPosition;
    }

    /// <summary>
    /// Writes content to stream
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteContent(Stream ms, HorseMessage message)
    {
        message.Content.Position = 0;

        if (message.Length > 0 && message.Content != null)
            message.Content.WriteTo(ms);

        if (message.HasAdditionalContent && message.AdditionalContent != null)
            message.AdditionalContent.WriteTo(ms);

        ms.Position = 0;
    }
}