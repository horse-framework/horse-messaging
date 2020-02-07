using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    public class DataMessageSerializer
    {
        private readonly TmqReader _reader = new TmqReader();

        #region Read

        public async Task<DataMessage> Read(Stream stream)
        {
            DataType type = ReadType(stream);
            string id = await ReadId(stream);

            if (type == DataType.Delete)
                return new DataMessage(DataType.Delete, id);

            int size = await ReadLength(stream);
            if (size == 0)
                return new DataMessage(DataType.Empty, null);

            TmqMessage msg = await _reader.Read(stream);
            return new DataMessage(DataType.Insert, id, msg);
        }

        internal DataType ReadType(Stream stream)
        {
            int b = stream.ReadByte();
            return (DataType) b;
        }

        internal async Task<string> ReadId(Stream stream)
        {
            int size = stream.ReadByte();
            byte[] data = new byte[size];
            int read = await stream.ReadAsync(data, 0, data.Length);

            if (read < data.Length)
                return null;

            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        internal async Task<int> ReadLength(Stream stream)
        {
            byte[] lb = new byte[4];
            int read = await stream.ReadAsync(lb, 0, lb.Length);
            if (read < lb.Length)
                return 0;

            return BitConverter.ToInt32(lb);
        }

        internal async Task<bool> ReadIntoContent(Stream stream, Stream readInto)
        {
            byte[] lb = new byte[4];
            int read = await stream.ReadAsync(lb, 0, lb.Length);
            if (read < lb.Length)
                return false;

            int size = BitConverter.ToInt32(lb);
            byte[] buffer = new byte[256];
            int left = size;
            while (left > 0)
            {
                int l = left > buffer.Length ? buffer.Length : left;
                read = await stream.ReadAsync(buffer, 0, l);
                if (read == 0)
                    return false;

                left -= read;
                await readInto.WriteAsync(buffer, 0, read);
            }

            return true;
        }

        #endregion

        #region Write

        public async Task Write(Stream stream, TmqMessage message)
        {
            WriteType(stream, DataType.Insert);
            await WriteId(stream, message.MessageId);
            message.Content.Position = 0;
            await WriteContent(Convert.ToInt32(message.Length), message.Content, stream);
        }

        public async Task WriteDelete(Stream stream, TmqMessage message)
        {
            stream.WriteByte((byte) DataType.Delete);
            stream.WriteByte(Convert.ToByte(message.MessageIdLength));
            await stream.WriteAsync(Encoding.UTF8.GetBytes(message.MessageId));
        }

        public async Task WriteDelete(Stream stream, string messageId)
        {
            WriteType(stream, DataType.Delete);
            await WriteId(stream, messageId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteType(Stream stream, DataType type)
        {
            stream.WriteByte((byte) type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task WriteId(Stream stream, string id)
        {
            byte length = Convert.ToByte(Encoding.UTF8.GetByteCount(id));

            stream.WriteByte((byte) DataType.Delete);
            stream.WriteByte(length);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(id));
        }

        internal async Task<bool> WriteContent(int length, Stream from, Stream to)
        {
            await to.WriteAsync(BitConverter.GetBytes(length));

            int left = length;
            byte[] buffer = new byte[256];
            while (left > 0)
            {
                int l = left > buffer.Length ? buffer.Length : left;
                int read = await from.ReadAsync(buffer, 0, l);
                if (read == 0)
                    return false;
                left -= read;
                await to.WriteAsync(buffer, 0, read);
            }

            return true;
        }

        #endregion
    }
}