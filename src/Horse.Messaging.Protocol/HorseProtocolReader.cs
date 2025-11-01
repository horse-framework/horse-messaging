using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Horse Protocol reader
/// </summary>
public class HorseProtocolReader
{
    private const int REQUIRED_SIZE = 8;
    private const int BUFFER_SIZE = 1024;
    private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Reads Horse message from stream
    /// </summary>
    public async ValueTask<HorseMessage> Read(Stream stream)
    {
        byte[] bytes = new byte[REQUIRED_SIZE];
        bool done = await ReadCertainBytes(stream, bytes, 0, REQUIRED_SIZE);
        if (!done)
            return null;

        HorseMessage message = new HorseMessage();
        done = await ReadFrame(message, bytes, stream);
        if (!done)
            return null;

        if (message.HasHeader)
            done = await ReadHeader(message, stream);

        if (!done)
            return null;

        bool success = await ReadContent(message, stream);
        if (!success)
            return null;

        if (message.HasAdditionalContent)
        {
            success = await ReadAdditionalContent(message, stream);
            if (!success)
                return null;

            if (message.AdditionalContent != null)
                message.AdditionalContent.Position = 0;
        }

        if (message.Content != null && message.Content.Position > 0)
            message.Content.Position = 0;

        return message;
    }

    /// <summary>
    /// Reads and process required frame data of the message
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<bool> ReadFrame(HorseMessage message, byte[] bytes, Stream stream)
    {
        byte proto = bytes[0];
        if (proto >= 128)
        {
            message.WaitResponse = true;
            proto -= 128;
        }

        if (proto >= 64)
        {
            message.HighPriority = true;
            proto -= 64;
        }

        if (proto >= 32)
        {
            proto -= 32;
            message.Type = (MessageType)proto;

            if (message.Type != MessageType.Ping && message.Type != MessageType.Pong)
            {
                message.HasHeader = true;
                message.HeadersList = new List<KeyValuePair<string, string>>();
            }
        }
        else
            message.Type = (MessageType)proto;

        message.HasAdditionalContent = bytes[1] > 127;
        message.MessageIdLength = bytes[2];
        message.SourceLength = bytes[3];
        message.TargetLength = bytes[4];

        message.ContentType = BitConverter.ToUInt16(bytes, 5);

        byte length = bytes[7];
        if (length == 253)
        {
            bool done = await ReadCertainBytes(stream, bytes, 0, 2);
            if (!done)
                return false;

            message.Length = BitConverter.ToUInt16(bytes, 0);
        }
        else if (length == 254)
        {
            bool done = await ReadCertainBytes(stream, bytes, 0, 4);
            if (!done)
                return false;

            message.Length = BitConverter.ToUInt32(bytes, 0);
        }
        else if (length == 255)
        {
            byte[] b = new byte[8];
            bool done = await ReadCertainBytes(stream, b, 0, 8);
            if (!done)
                return false;

            message.Length = BitConverter.ToUInt64(b, 0);
        }
        else
            message.Length = length;

        if (message.HasAdditionalContent)
        {
            byte[] additionalContentBytes = new byte[4];
            bool done = await ReadCertainBytes(stream, additionalContentBytes, 0, additionalContentBytes.Length);
            if (!done)
                return false;

            message.AdditionalContentLength = BitConverter.ToInt32(additionalContentBytes);
        }

        byte[] octetBuffer = _arrayPool.Rent(256);
        try
        {
            if (message.MessageIdLength > 0)
                message.MessageId = await ReadOctetSizeData(stream, octetBuffer, message.MessageIdLength);

            if (message.SourceLength > 0)
                message.Source = await ReadOctetSizeData(stream, octetBuffer, message.SourceLength);

            if (message.TargetLength > 0)
                message.Target = await ReadOctetSizeData(stream, octetBuffer, message.TargetLength);
        }
        finally
        {
            _arrayPool.Return(octetBuffer);
        }

        return true;
    }

    /// <summary>
    /// Reads and process header data of the message
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<bool> ReadHeader(HorseMessage message, Stream stream)
    {
        byte[] size = new byte[2];
        bool read = await ReadCertainBytes(stream, size, 0, size.Length);
        if (!read)
            return false;

        int headerLength = BitConverter.ToUInt16(size);
        byte[] data = _arrayPool.Rent(headerLength);

        try
        {
            read = await ReadCertainBytes(stream, data, 0, headerLength);
            if (!read)
                return false;

            ReadOnlySpan<byte> dataSpan = data.AsSpan(0, headerLength);
            int start = 0;

            for (int i = 0; i < dataSpan.Length - 1; i++)
            {
                if (dataSpan[i] == (byte)'\r' && dataSpan[i + 1] == (byte)'\n')
                {
                    if (i > start)
                    {
                        ReadOnlySpan<byte> line = dataSpan.Slice(start, i - start);
                        int colonIndex = line.IndexOf((byte)':');

                        if (colonIndex > 0)
                        {
                            string key = Encoding.UTF8.GetString(line[..colonIndex]);
                            string value = Encoding.UTF8.GetString(line[(colonIndex + 1)..]);
                            message.HeadersList.Add(new KeyValuePair<string, string>(key, value));
                        }
                    }

                    start = i + 2;
                    i++;
                }
            }
        }
        finally
        {
            _arrayPool.Return(data);
        }

        /*
        read = await ReadCertainBytes(stream, data, 0, data.Length);
        if (!read)
            return false;

        string[] headers = Encoding.UTF8.GetString(data).Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (string header in headers)
        {
            int i = header.IndexOf(':');
            if (i < 1)
                continue;

            string key = header.Substring(0, i);
            string value = header.Substring(i + 1);
            message.HeadersList.Add(new KeyValuePair<string, string>(key, value));
        }
        */

        return true;
    }

    /// <summary>
    /// Reads message content
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<bool> ReadContent(HorseMessage message, Stream stream)
    {
        if (message.Length == 0)
            return true;

        if (message.Content == null)
            message.Content = new MemoryStream();

        ulong left = message.Length;
        byte[] readBuffer = _arrayPool.Rent(BUFFER_SIZE);
        try
        {
            do
            {
                int readCount = (int)Math.Min(left, (ulong)BUFFER_SIZE);
                int read = await stream.ReadAsync(readBuffer.AsMemory(0, readCount));
                if (read == 0)
                    return false;

                left -= (uint)read;
                await message.Content.WriteAsync(readBuffer.AsMemory(0, read));
            } while (left > 0);
        }
        finally
        {
            _arrayPool.Return(readBuffer);
        }

        /*
        ulong left = message.Length;
        ulong blen = (ulong) _buffer.Length;
        do
        {
            int rcount = (int) (left > blen ? blen : left);
            int read = await stream.ReadAsync(_buffer, 0, rcount);
            if (read == 0)
                return false;

            left -= (uint) read;
            await message.Content.WriteAsync(_buffer, 0, read);
        } while (left > 0);
        */

        return true;
    }

    /// <summary>
    /// Reads message content
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<bool> ReadAdditionalContent(HorseMessage message, Stream stream)
    {
        if (message.AdditionalContentLength == 0)
            return true;

        if (message.AdditionalContent == null)
            message.AdditionalContent = new MemoryStream();

        int left = message.AdditionalContentLength;
        byte[] readBuffer = _arrayPool.Rent(BUFFER_SIZE);
        try
        {
            do
            {
                int readCount = left > BUFFER_SIZE ? BUFFER_SIZE : left;
                int read = await stream.ReadAsync(readBuffer, 0, readCount);
                if (read == 0)
                    return false;

                left -= read;
                await message.AdditionalContent.WriteAsync(readBuffer, 0, read);
            } while (left > 0);
        }
        finally
        {
            _arrayPool.Return(readBuffer);
        }

        return true;
    }

    /// <summary>
    /// Reads octet size data and returns as string
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<string> ReadOctetSizeData(Stream stream, byte[] buffer, int length)
    {
        bool done = await ReadCertainBytes(stream, buffer, 0, length);
        return done ? Encoding.UTF8.GetString(buffer, 0, length) : "";
    }

    /// <summary>
    /// Reads length bytes from the stream, not even one byte less.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<bool> ReadCertainBytes(Stream stream, byte[] buffer, int start, int length)
    {
        int total = 0;
        do
        {
            int read = await stream.ReadAsync(buffer.AsMemory(start + total, length - total));
            if (read == 0)
                return false;

            total += read;
        } while (total < length);

        return true;
    }
}