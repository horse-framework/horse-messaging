using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Protocols;

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
            await using MemoryStream ms = new MemoryStream();
            await WriteFrame(ms, value);
            WriteContent(ms, value);
            ms.WriteTo(stream);
        }

        /// <summary>
        /// Creates byte array of TMQ message
        /// </summary>
        public async Task<byte[]> Create(TmqMessage value)
        {
            await using MemoryStream ms = new MemoryStream();
            await WriteFrame(ms, value);
            WriteContent(ms, value);
            return ms.ToArray();
        }

        /// <summary>
        /// Creates byte array of only TMQ message frame
        /// </summary>
        public async Task<byte[]> CreateFrame(TmqMessage value)
        {
            await using MemoryStream ms = new MemoryStream();
            await WriteFrame(ms, value);
            return ms.ToArray();
        }

        /// <summary>
        /// Creates byte array of only TMQ message content
        /// </summary>
        public async Task<byte[]> CreateContent(TmqMessage value)
        {
            await using MemoryStream ms = new MemoryStream();
            WriteContent(ms, value);
            return ms.ToArray();
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
            
            if (message.DeliveryRequired)
                ttl += 64;

            ms.WriteByte(ttl);

            ms.WriteByte((byte) message.MessageIdLength);
            ms.WriteByte((byte) message.SourceLength);
            ms.WriteByte((byte) message.TargetLength);

            byte[] cbytes = BitConverter.GetBytes(message.ContentType);
            await ms.WriteAsync(new[] {cbytes[1], cbytes[0]});

            if (message.Length < 253)
                ms.WriteByte((byte) message.Length);
            else if (message.Length <= ushort.MaxValue)
            {
                ushort len = (ushort) message.Length;
                byte[] lenbytes = BitConverter.GetBytes(len);
                await ms.WriteAsync(new[] {lenbytes[1], lenbytes[0]});
            }
            else if (message.Length <= uint.MaxValue)
            {
                byte[] lb = BitConverter.GetBytes((uint) message.Length);
                await ms.WriteAsync(new[] {lb[3], lb[2], lb[1], lb[0]});
            }
            else
            {
                byte[] lb = BitConverter.GetBytes(message.Length);
                await ms.WriteAsync(new[] {lb[7], lb[6], lb[5], lb[4], lb[3], lb[2], lb[1], lb[0]});
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