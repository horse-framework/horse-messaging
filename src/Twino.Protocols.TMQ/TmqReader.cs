using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Protocol reader
    /// </summary>
    public class TmqReader
    {
        /// <summary>
        /// Buffer. Should be at least 256 bytes (reading once some values smaller 257 bytes)
        /// </summary>
        private readonly byte[] _buffer = new byte[256];

        private const int REQUIRED_SIZE = 8;

        /// <summary>
        /// Reads TMQ message from stream
        /// </summary>
        public async Task<TwinoMessage> Read(Stream stream)
        {
            byte[] bytes = new byte[REQUIRED_SIZE];
            bool done = await ReadCertainBytes(stream, bytes, 0, REQUIRED_SIZE);
            if (!done)
                return null;

            TwinoMessage message = new TwinoMessage();
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

            if (message.Content != null && message.Content.Position > 0)
                message.Content.Position = 0;

            return message;
        }

        /// <summary>
        /// Reads and process required frame data of the message
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<bool> ReadFrame(TwinoMessage message, byte[] bytes, Stream stream)
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
                message.Type = (MessageType) proto;

                if (message.Type != MessageType.Ping && message.Type != MessageType.Pong)
                {
                    message.HasHeader = true;
                    message.HeadersList = new List<KeyValuePair<string, string>>();
                }
            }
            else
                message.Type = (MessageType) proto;

            // bytes[1] is reserved

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

            if (message.MessageIdLength > 0)
                message.MessageId = await ReadOctetSizeData(stream, message.MessageIdLength);

            if (message.SourceLength > 0)
                message.Source = await ReadOctetSizeData(stream, message.SourceLength);

            if (message.TargetLength > 0)
                message.Target = await ReadOctetSizeData(stream, message.TargetLength);

            return true;
        }

        /// <summary>
        /// Reads and process header data of the message
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<bool> ReadHeader(TwinoMessage message, Stream stream)
        {
            byte[] size = new byte[2];
            bool read = await ReadCertainBytes(stream, size, 0, size.Length);
            if (!read)
                return false;

            int headerLength = BitConverter.ToUInt16(size);
            byte[] data = new byte[headerLength];

            read = await ReadCertainBytes(stream, data, 0, data.Length);
            if (!read)
                return false;

            string[] headers = Encoding.UTF8.GetString(data).Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string header in headers)
            {
                int i = header.IndexOf(':');
                if (i < 1)
                    continue;

                string key = header.Substring(0, i);
                string value = header.Substring(i + 1);
                message.HeadersList.Add(new KeyValuePair<string, string>(key, value));
            }

            return true;
        }

        /// <summary>
        /// Reads message content
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<bool> ReadContent(TwinoMessage message, Stream stream)
        {
            if (message.Length == 0)
                return true;

            if (message.Content == null)
                message.Content = new MemoryStream();

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
            }
            while (left > 0);

            return true;
        }

        /// <summary>
        /// Reads octet size data and returns as string
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<string> ReadOctetSizeData(Stream stream, int length)
        {
            bool done = await ReadCertainBytes(stream, _buffer, 0, length);
            return done ? Encoding.UTF8.GetString(_buffer, 0, length) : "";
        }

        /// <summary>
        /// Reads length bytes from the stream, not even one byte less.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<bool> ReadCertainBytes(Stream stream, byte[] buffer, int start, int length)
        {
            int total = 0;
            do
            {
                int read = await stream.ReadAsync(buffer, start + total, length - total);
                if (read == 0)
                    return false;

                total += read;
            }
            while (total < length);

            return true;
        }
    }
}