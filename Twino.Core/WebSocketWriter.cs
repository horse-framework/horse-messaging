using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Core
{
    /// <summary>
    /// Creates websocket protocol messages from the plain messages
    /// </summary>
    public class WebSocketWriter
    {
        /// <summary>
        /// Creates new webosocket protocol message of a string value
        /// </summary>
        public static byte[] CreateFromUTF8(string message)
        {
            using MemoryStream ms = new MemoryStream();
            //fin and op code
            ms.WriteByte(0x81);
            byte[] msg = Encoding.UTF8.GetBytes(message);

            //message length
            WriteLength(ms, msg.Length);

            ms.Write(msg, 0, msg.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// Creates new webosocket protocol message of a string value
        /// </summary>
        public static async Task<byte[]> CreateFromUTF8Async(string message)
        {
            await using MemoryStream ms = new MemoryStream();
            
            //fin and op code
            ms.WriteByte(0x81);
            byte[] msg = Encoding.UTF8.GetBytes(message);

            //message length
            await WriteLengthAsync(ms, msg.Length);

            await ms.WriteAsync(msg, 0, msg.Length);
            return ms.ToArray();
        }
        
        /// <summary>
        /// Creates new websocket protocol message of a binary  value
        /// </summary>
        public static byte[] CreateFromBinary(byte[] data)
        {
            using MemoryStream ms = new MemoryStream();
            //fin and op code
            ms.WriteByte(0x82);

            //length
            WriteLength(ms, data.Length);

            ms.Write(data, 0, data.Length);
            return ms.ToArray();
        }
        /// <summary>
        /// Creates new websocket protocol message of a binary  value
        /// </summary>
        public static async Task<byte[]> CreateFromBinaryAsync(byte[] data)
        {
            await using MemoryStream ms = new MemoryStream();
            //fin and op code
            ms.WriteByte(0x82);

            //length
            await WriteLengthAsync(ms, data.Length);

            await ms.WriteAsync(data, 0, data.Length);
            return ms.ToArray();
        }
        
        /// <summary>
        /// Creates new ping message
        /// </summary>
        public static byte[] CreatePing()
        {
            byte[] result = {0x89, 0x00};
            return result;
        }

        /// <summary>
        /// Creates new ping message
        /// </summary>
        public static byte[] CreatePong()
        {
            byte[] result = {0x8A, 0x00};
            return result;
        }

        /// <summary>
        /// Writes length of the message with websocket protocol
        /// </summary>
        private static void WriteLength(MemoryStream stream, int length)
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
                stream.Write(new[] {lenbytes[1], lenbytes[0]}, 0, 2);
            }

            //9 (1 + ulong) bytes length
            else
            {
                stream.WriteByte(127);
                ulong len = (ulong) length;
                byte[] lb = BitConverter.GetBytes(len);
                stream.Write(new[] {lb[7], lb[6], lb[5], lb[4], lb[3], lb[2], lb[1], lb[0]}, 0, 8);
            }
        }
        
        /// <summary>
        /// Writes length of the message with websocket protocol
        /// </summary>
        private static async Task WriteLengthAsync(MemoryStream stream, int length)
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
                ulong len = (ulong) length;
                byte[] lb = BitConverter.GetBytes(len);
                await stream.WriteAsync(new[] {lb[7], lb[6], lb[5], lb[4], lb[3], lb[2], lb[1], lb[0]}, 0, 8);
            }
        }
    }
}