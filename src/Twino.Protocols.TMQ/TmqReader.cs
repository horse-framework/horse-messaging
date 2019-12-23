using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Protocols.TMQ
{
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
        public async Task<TmqMessage> Read(Stream stream)
        {
            byte[] bytes = new byte[REQUIRED_SIZE];
            bool done = await ReadCertainBytes(stream, bytes, 0, REQUIRED_SIZE);
            if (!done)
                return null;

            TmqMessage message = new TmqMessage();
            done = await ProcessRequiredFrame(message, bytes, stream);
            if (!done)
                return null;
            
            message.Ttl--;

            await ReadContent(message, stream);

            if (message.Content != null && message.Content.Position > 0)
                message.Content.Position = 0;

            return message;
        }

        /// <summary>
        /// Process required frame data of message
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<bool> ProcessRequiredFrame(TmqMessage message, byte[] bytes, Stream stream)
        {
            byte type = bytes[0];
            if (type >= 128)
            {
                message.FirstAcquirer = true;
                type -= 128;
            }

            if (type >= 64)
            {
                message.HighPriority = true;
                type -= 64;
            }

            message.Type = (MessageType) type;

            byte ttl = bytes[1];
            if (ttl >= 128)
            {
                message.ResponseRequired = true;
                ttl -= 128;
            }

            if (ttl >= 64)
            {
                message.AcknowledgeRequired = true;
                ttl -= 64;
            }

            message.Ttl = ttl;

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

            return true;
        }

        /// <summary>
        /// Reads message content
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task ReadContent(TmqMessage message, Stream stream)
        {
            if (message.MessageIdLength > 0)
                message.MessageId = await ReadOctetSizeData(stream, message.MessageIdLength);

            if (message.SourceLength > 0)
                message.Source = await ReadOctetSizeData(stream, message.SourceLength);

            if (message.TargetLength > 0)
                message.Target = await ReadOctetSizeData(stream, message.TargetLength);

            if (message.Length == 0)
                return;

            if (message.Content == null)
                message.Content = new MemoryStream();

            ulong left = message.Length;
            ulong blen = (ulong) _buffer.Length;
            do
            {
                int rcount = (int) (left > blen ? blen : left);
                int read = await stream.ReadAsync(_buffer, 0, rcount);
                left -= (uint) read;
                await message.Content.WriteAsync(_buffer, 0, read);
            } while (left > 0);
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
            } while (total < length);

            return true;
        }
    }
}