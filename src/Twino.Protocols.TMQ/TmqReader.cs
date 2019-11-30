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

            return message;
        }

        /// <summary>
        /// Reads required frame of the message
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<byte[]> ReadRequiredFrame(Stream stream)
        {
            byte[] bytes = new byte[REQUIRED_SIZE];

            int read = await stream.ReadAsync(bytes, 0, bytes.Length);
            if (read == 0)
                return null;

            if (read < REQUIRED_SIZE)
            {
                int reread = await stream.ReadAsync(bytes, read, bytes.Length - REQUIRED_SIZE);
                if (reread + read < REQUIRED_SIZE)
                    return null;
            }

            return bytes;
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

            message.ContentType = BitConverter.ToUInt16(new[] {bytes[6], bytes[5]}, 0);

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
                byte[] lbytes = new byte[8];
                await stream.ReadAsync(lbytes, 0, 8);
                message.Length = BitConverter.ToUInt64(lbytes, 0);
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

            ulong total = 0;
            do
            {
                int read = await stream.ReadAsync(_buffer, 0, _buffer.Length);
                total += (uint) read;
                await message.Content.WriteAsync(_buffer, 0, read);
            } while (total < message.Length);
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
    }
}