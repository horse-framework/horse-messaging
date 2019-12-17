using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Message writer
    /// </summary>
    public class TmqWriter
    {
        /// <summary>
        /// Writes a TMQ message to stream
        /// </summary>
        public async Task Write(TmqMessage value, Stream stream)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await WriteFrame(ms, value);
                WriteContent(ms, value);
                ms.WriteTo(stream);
            }
        }

        /// <summary>
        /// Creates byte array of TMQ message
        /// </summary>
        public async Task<byte[]> Create(TmqMessage value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await WriteFrame(ms, value);
                
                if (value.MessageIdLength > 0)
                    WriteContent(ms, value);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Creates byte array of only TMQ message frame
        /// </summary>
        public async Task<byte[]> CreateFrame(TmqMessage value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await WriteFrame(ms, value);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Creates byte array of only TMQ message content
        /// </summary>
        public async Task<byte[]> CreateContent(TmqMessage value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteContent(ms, value);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes frame to stream
        /// </summary>
        private static async Task WriteFrame(MemoryStream ms, TmqMessage message)
        {
            byte type = (byte) message.Type;
            if (message.FirstAcquirer)
                type += 128;
            if (message.HighPriority)
                type += 64;

            ms.WriteByte(type);

            byte ttl = (byte) message.Ttl;
            if (ttl > 63)
                ttl = 63;

            if (message.ResponseRequired)
                ttl += 128;

            if (message.AcknowledgeRequired)
                ttl += 64;

            ms.WriteByte(ttl);

            ms.WriteByte((byte) message.MessageIdLength);
            ms.WriteByte((byte) message.SourceLength);
            ms.WriteByte((byte) message.TargetLength);

            await ms.WriteAsync(BitConverter.GetBytes(message.ContentType));

            if (message.Length < 253)
                ms.WriteByte((byte) message.Length);
            else if (message.Length <= ushort.MaxValue)
            {
                ms.WriteByte(253);
                await ms.WriteAsync(BitConverter.GetBytes((ushort) message.Length));
            }
            else if (message.Length <= uint.MaxValue)
            {
                ms.WriteByte(254);
                await ms.WriteAsync(BitConverter.GetBytes((uint) message.Length));
            }
            else
            {
                ms.WriteByte(255);
                await ms.WriteAsync(BitConverter.GetBytes(message.Length));
            }

            if (message.MessageIdLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.MessageId);
                await ms.WriteAsync(bytes);
            }

            if (message.SourceLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.Source);
                await ms.WriteAsync(bytes);
            }

            if (message.TargetLength > 0)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message.Target);
                await ms.WriteAsync(bytes);
            }
        }

        /// <summary>
        /// Writes content to stream
        /// </summary>
        private static void WriteContent(MemoryStream ms, TmqMessage message)
        {
            if (message.Length > 0 && message.Content != null)
                message.Content.WriteTo(ms);

            ms.Position = 0;
        }
    }
}