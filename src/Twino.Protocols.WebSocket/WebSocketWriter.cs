using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Protocols.WebSocket
{
    /// <summary>
    /// WebSocket Protocol message writer
    /// </summary>
    public class WebSocketWriter
    {
        /// <summary>
        /// Writes message to specified stream.
        /// </summary>
        public async Task Write(WebSocketMessage value, Stream stream)
        {
            //fin and op code
            stream.WriteByte(value.OpCode == SocketOpCode.Binary ? (byte) 0x82 : (byte) 0x81);

            //length
            await WriteLengthAsync(stream, (ulong) value.Content.Length);
            value.Content.WriteTo(stream);
        }

        /// <summary>
        /// Creates byte array data of the message
        /// </summary>
        public async Task<byte[]> Create(WebSocketMessage value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                //fin and op code
                ms.WriteByte(value.OpCode == SocketOpCode.Binary ? (byte) 0x82 : (byte) 0x81);

                //length
                await WriteLengthAsync(ms, (ulong) value.Content.Length);

                value.Content.WriteTo(ms);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Creates byte array data of the message
        /// </summary>
        public async Task<byte[]> Create(string message)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x81);
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await WriteLengthAsync(ms, (ulong) bytes.Length);
                await ms.WriteAsync(bytes);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Creates byte array data of only message header frame
        /// </summary>
        public async Task<byte[]> CreateFrame(WebSocketMessage value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                //fin and op code
                ms.WriteByte(value.OpCode == SocketOpCode.Binary ? (byte) 0x82 : (byte) 0x81);

                //length
                await WriteLengthAsync(ms, (ulong) value.Content.Length);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Creates byte array data of only message content
        /// </summary>
        public async Task<byte[]> CreateContent(WebSocketMessage value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                value.Content.WriteTo(ms);
                return await Task.FromResult(ms.ToArray());
            }
        }

        /// <summary>
        /// Writes length of the message with websocket protocol
        /// </summary>
        private static async Task WriteLengthAsync(Stream stream, ulong length)
        {
            //1 byte length
            if (length < 126)
                stream.WriteByte((byte) length);

            //3 (1 + ushort) bytes length
            else if (length <= UInt16.MaxValue)
            {
                stream.WriteByte(126);
                ushort len = (ushort) length;
                byte[] lenbytes = BitConverter.GetBytes(len);
                await stream.WriteAsync(new[] {lenbytes[1], lenbytes[0]}, 0, 2);
            }

            //9 (1 + ulong) bytes length
            else
            {
                stream.WriteByte(127);
                ulong len = length;
                byte[] lb = BitConverter.GetBytes(len);
                await stream.WriteAsync(new[] {lb[7], lb[6], lb[5], lb[4], lb[3], lb[2], lb[1], lb[0]}, 0, 8);
            }
        }
    }
}