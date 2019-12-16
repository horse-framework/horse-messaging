using System;
using System.IO;
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
            byte[] bytes = await ReadRequiredFrame(stream);
            if (bytes == null || bytes.Length < REQUIRED_SIZE)
                return null;

            TmqMessage message = new TmqMessage();
            await ProcessRequiredFrame(message, bytes, stream);
            message.Ttl--;

            await ReadContent(message, stream);

            if (message.Content != null && message.Content.Position > 0)
                message.Content.Position = 0;

            return message;
        }

        /// <summary>
        /// Reads required frame of the message
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<byte[]> ReadRequiredFrame(Stream stream)
        {
            byte[] bytes = new byte[REQUIRED_SIZE];
            bool done = await ReadCertainBytes(stream, bytes, 0, REQUIRED_SIZE);
            return !done ? null : bytes;
        }

        /// <summary>
        /// Process required frame data of message
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task ProcessRequiredFrame(TmqMessage message, byte[] bytes, Stream stream)
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
                await stream.ReadAsync(bytes, 0, 2);
                message.Length = BitConverter.ToUInt16(bytes, 0);
            }
            else if (length == 254)
            {
                await stream.ReadAsync(bytes, 0, 4);
                message.Length = BitConverter.ToUInt32(bytes, 0);
            }
            else if (length == 255)
            {
                byte[] b = new byte[8];
                await stream.ReadAsync(b, 0, 8);
                message.Length = BitConverter.ToUInt64(b, 0);
            }
            else
                message.Length = length;
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
            int read = await stream.ReadAsync(_buffer, 0, length);
            if (read == length)
                return Encoding.UTF8.GetString(_buffer, 0, length);

            string result = "";
            int total = read;
            do
            {
                result += Encoding.UTF8.GetString(_buffer, 0, read);
                read = await stream.ReadAsync(_buffer, 0, length);
                total += read;
            } while (total >= length);

            return result;
        }

        /// <summary>
        /// Reads length bytes from the stream, not even one byte less.
        /// </summary>
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