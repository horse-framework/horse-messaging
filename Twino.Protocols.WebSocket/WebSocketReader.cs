using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    public class WebSocketReader : IProtocolMessageReader<WebSocketMessage>
    {
        private readonly byte[] _buffer = new byte[128];

        public ProtocolHandshakeResult HandshakeResult { get; set; }

        public void Reset()
        {
        }

        public async Task<WebSocketMessage> Read(Stream stream)
        {
            byte[] frames = new byte[2];
            int read = await stream.ReadAsync(frames, 0, frames.Length);
            if (read < 2)
                return null;

            WebSocketMessage message = new WebSocketMessage();

            byte code = frames[0];
            if (code > 127)
                code -= 128;
            
            message.OpCode = (SocketOpCode) code;

            byte maskbyte = frames[1];
            if (maskbyte > 127)
            {
                message.Masking = true;
                maskbyte -= 128;
            }

            message.Length = await ReadLength(maskbyte, stream);

            if (message.Masking)
                message.Mask = await ReadMask(stream);

            await ReadContent(stream, message);

            return message;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<ulong> ReadLength(byte first, Stream stream)
        {
            //reads 1 byte length
            if (first < 126)
                return first;

            //reads 3 (1 + short) bytes length
            if (first == 126)
            {
                byte[] sbytes = new byte[2];
                await stream.ReadAsync(sbytes, 0, 2);
                return BitConverter.ToUInt16(sbytes, 0);
            }

            //reads 9 (1 + long) bytes length
            if (first == 127)
            {
                byte[] sbytes = new byte[8];
                await stream.ReadAsync(sbytes, 0, 8);
                return BitConverter.ToUInt64(sbytes, 0);
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<byte[]> ReadMask(Stream stream)
        {
            byte[] mask = new byte[4];
            await stream.ReadAsync(mask, 0, mask.Length);
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task ReadContent(Stream stream, WebSocketMessage message)
        {
            ulong total = 0;
            if (message.Content == null)
                message.Content = new MemoryStream();

            do
            {
                int size = _buffer.Length;
                if (total + (uint) size > message.Length)
                    size = (int) (message.Length - total);

                int read = await stream.ReadAsync(_buffer, 0, size);
                total += (uint) read;

                if (message.Masking)
                    for (int i = 0; i < read; i++)
                        _buffer[i] = (byte) (_buffer[i] ^ message.Mask[i % 4]);

                await message.Content.WriteAsync(_buffer, 0, read);
            } while (total < message.Length);
        }
    }
}