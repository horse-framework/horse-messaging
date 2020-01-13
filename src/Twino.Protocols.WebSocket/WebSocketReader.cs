using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Twino.Core.Protocols;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// WebSocket Protocol message reader
    /// </summary>
    public class WebSocketReader
    {
        /// <summary>
        /// Buffer. 128 not a specific number, can be changed.
        /// </summary>
        private readonly byte[] _buffer = new byte[128];

        /// <summary>
        /// Handshake result for websocket protocol.
        /// This value created right after 101 switching protocols response.
        /// Includes HTTP request's information.
        /// </summary>
        public ProtocolHandshakeResult HandshakeResult { get; set; }

        /// <summary>
        /// Resets reader status for next reading operation.
        /// Method is empty, because Websocket reader does not require any reset operation.
        /// </summary>
        public void Reset()
        {
        }

        /// <summary>
        /// Reads a WebSocketMessage from stream
        /// </summary>
        public async Task<WebSocketMessage> Read(Stream stream)
        {
            byte[] frames = new byte[2];
            bool done = await ReadCertainBytes(stream, frames, 0, 2);
            if (!done)
                return null;

            WebSocketMessage message = new WebSocketMessage();

            byte code = frames[0];
            if (code > 127)
                code -= 128;

            message.OpCode = (SocketOpCode) code;
            if (message.OpCode == SocketOpCode.Terminate)
                return null;

            byte maskbyte = frames[1];
            if (maskbyte > 127)
            {
                message.Masking = true;
                maskbyte -= 128;
            }

            long length = await ReadLength(maskbyte, stream);

            if (message.Masking)
            {
                message.Mask = new byte[4];
                done = await ReadCertainBytes(stream, message.Mask, 0, 4);
                if (!done)
                    return null;
            }

            if (length == 0)
                return message;

            bool success = await ReadContent(stream, message, length);
            return success ? message : null;
        }

        /// <summary>
        /// Reads websocket protocol content length from stream 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task<long> ReadLength(byte first, Stream stream)
        {
            //reads 1 byte length
            if (first < 126)
                return first;

            //reads 3 (1 + short) bytes length
            if (first == 126)
            {
                byte[] sbytes = new byte[2];
                bool done = await ReadCertainBytes(stream, sbytes, 0, 2);
                if (!done)
                    throw new SocketException();

                return BitConverter.ToUInt16(new[] {sbytes[1], sbytes[0]}, 0);
            }

            //reads 9 (1 + long) bytes length
            if (first == 127)
            {
                byte[] sbytes = new byte[8];
                bool done = await ReadCertainBytes(stream, sbytes, 0, 8);
                if (!done)
                    throw new SocketException();

                return BitConverter.ToInt64(new[] {sbytes[7], sbytes[6], sbytes[5], sbytes[4], sbytes[3], sbytes[2], sbytes[1], sbytes[0]}, 0);
            }

            return 0;
        }

        /// <summary>
        /// Reads message content from stream
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<bool> ReadContent(Stream stream, WebSocketMessage message, long length)
        {
            long total = 0;
            if (message.Content == null)
                message.Content = new MemoryStream();

            do
            {
                long size = _buffer.Length;
                if (total + size > length)
                    size = (length - total);

                int read = await stream.ReadAsync(_buffer, 0, (int) size);
                if (read == 0)
                    return false;

                total += read;

                if (message.Masking)
                    for (int i = 0; i < read; i++)
                        _buffer[i] = (byte) (_buffer[i] ^ message.Mask[i % 4]);

                await message.Content.WriteAsync(_buffer, 0, read);
            } while (total < length);

            return true;
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