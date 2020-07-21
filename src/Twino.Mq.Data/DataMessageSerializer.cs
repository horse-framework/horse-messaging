using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Message serializer object.
    /// Serializes messages for keeping in database file.
    /// And deserializes them to extract from file to queue usage. 
    /// </summary>
    public class DataMessageSerializer
    {
        /// <summary>
        /// Default TMQ Protocol reader
        /// </summary>
        private readonly TmqReader _reader = new TmqReader();

        /// <summary>
        /// Creates new data message serializer
        /// </summary>
        public DataMessageSerializer()
        {
            _reader.DecreaseTTL = false;
        }

        #region Read

        /// <summary>
        /// Reads a data message object from the stream
        /// </summary>
        public async Task<DataMessage> Read(Stream stream)
        {
            DataType type = ReadType(stream);
            string id = await ReadId(stream);

            if (string.IsNullOrEmpty(id))
                return new DataMessage(DataType.Empty, null);

            if (type == DataType.Delete)
                return new DataMessage(DataType.Delete, id);

            int size = await ReadLength(stream);
            if (size == 0)
                return new DataMessage(DataType.Empty, id);

            TmqMessage msg = await _reader.Read(stream);
            return new DataMessage(DataType.Insert, id, msg);
        }

        /// <summary>
        /// Reads only message type from stream
        /// </summary>
        internal DataType ReadType(Stream stream)
        {
            int b = stream.ReadByte();
            return (DataType) b;
        }

        /// <summary>
        /// Reads id length byte and id itself from stream
        /// </summary>
        internal async Task<string> ReadId(Stream stream)
        {
            int size = stream.ReadByte();
            if (size > 256)
                return null;

            byte[] data = new byte[size];
            int read = await stream.ReadAsync(data, 0, data.Length);

            if (read < data.Length)
                return null;

            return Encoding.UTF8.GetString(data, 0, data.Length);
        }

        /// <summary>
        /// Reads message length from stream
        /// </summary>
        internal async Task<int> ReadLength(Stream stream)
        {
            byte[] lb = new byte[4];
            int read = await stream.ReadAsync(lb, 0, lb.Length);
            if (read < lb.Length)
                return 0;

            return BitConverter.ToInt32(lb);
        }

        /// <summary>
        /// Reads a content with it's length bytes from a stream and writes the data to another stream
        /// </summary>
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

        /// <summary>
        /// Writes a message to a stream
        /// </summary>
        public async Task Write(Stream stream, TmqMessage message)
        {
            WriteType(stream, DataType.Insert);
            await WriteId(stream, message.MessageId);
            message.Content.Position = 0;

            await using MemoryStream ms = new MemoryStream();
            TmqWriter.Write(message, ms);
            ms.Position = 0;

            await WriteContent(Convert.ToInt32(ms.Length), ms, stream);
        }

        /// <summary>
        /// Write message delete opereation to the stream
        /// </summary>
        public async Task WriteDelete(Stream stream, TmqMessage message)
        {
            stream.WriteByte((byte) DataType.Delete);
            stream.WriteByte(Convert.ToByte(message.MessageIdLength));
            await stream.WriteAsync(Encoding.UTF8.GetBytes(message.MessageId));
        }

        /// <summary>
        /// Write message delete opereation to the stream
        /// </summary>
        public async Task WriteDelete(Stream stream, string messageId)
        {
            WriteType(stream, DataType.Delete);
            await WriteId(stream, messageId);
        }

        /// <summary>
        /// Writes message data type to the stream
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteType(Stream stream, DataType type)
        {
            stream.WriteByte((byte) type);
        }

        /// <summary>
        /// Writes message id length and id value to the stream
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task WriteId(Stream stream, string id)
        {
            byte length = Convert.ToByte(Encoding.UTF8.GetByteCount(id));

            stream.WriteByte(length);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(id));
        }

        /// <summary>
        /// Writes specified bytes content to target stream from source stream
        /// </summary>
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