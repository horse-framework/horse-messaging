using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.MQ.Models;

namespace Twino.MQ.Core
{
    public class MessageWriter
    {
        public async Task Write(QueueMessage message, Stream stream)
        {
            await using MemoryStream ms = new MemoryStream();
            await WriteFrame(ms, message);
            WriteContent(ms, message);
            
            ms.WriteTo(stream);
        }

        public async Task<byte[]> PrepareBytes(QueueMessage message)
        {
            await using MemoryStream ms = new MemoryStream();
            await WriteFrame(ms, message);
            WriteContent(ms, message);
            
            return ms.ToArray();
        }

        public async Task WriteFrame(MemoryStream ms, QueueMessage message)
        {
            byte type = (byte) message.Type;
            if (message.FirstAcquirer)
                type += 128;
            if (message.HighPriority)
                type += 64;

            ms.WriteByte(type);

            byte ttl = (byte) message.Ttl;
            if (message.ResponseRequired)
                ttl += 128;

            ms.WriteByte(ttl);

            ms.WriteByte((byte) message.MessageIdLength);
            ms.WriteByte((byte) message.SourceLength);
            ms.WriteByte((byte) message.TargetLength);

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

        public void WriteContent(MemoryStream ms, QueueMessage message)
        {
            if (message.Length > 0 && message.Content != null)
                message.Content.WriteTo(ms);

            ms.Position = 0;
        }
    }
}