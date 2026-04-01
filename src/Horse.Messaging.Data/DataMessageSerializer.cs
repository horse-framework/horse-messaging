using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Data;

/// <summary>
/// Message serializer object.
/// Serializes messages for keeping in database file.
/// And deserializes them to extract from file to queue usage. 
/// </summary>
public class DataMessageSerializer
{
    /// <summary>
    /// Default Horse Protocol reader
    /// </summary>
    private readonly HorseProtocolReader _reader = new();

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
        if (size <= 0)
            return new DataMessage(DataType.Empty, id);

        long recordStart = stream.CanSeek ? stream.Position : 0;

        try
        {
            using BoundedReadStream bounded = new BoundedReadStream(stream, size);
            HorseMessage msg = await _reader.Read(bounded);

            if (stream.CanSeek)
                stream.Position = recordStart + size;

            return msg == null
                ? new DataMessage(DataType.Empty, id)
                : new DataMessage(DataType.Insert, id, msg);
        }
        catch
        {
            if (stream.CanSeek)
                stream.Position = recordStart + size;

            return new DataMessage(DataType.Empty, id);
        }
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
        if (size < 0 || size > byte.MaxValue)
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
    public async Task Write(Stream stream, HorseMessage message)
    {
        WriteType(stream, DataType.Insert);
        await WriteId(stream, message.MessageId);
        message.Content?.Position = 0;

        await using MemoryStream ms = new MemoryStream();
        HorseProtocolWriter.Write(message, ms);
        ms.Position = 0;

        await WriteContent(Convert.ToInt32(ms.Length), ms, stream);
    }

    /// <summary>
    /// Write message delete opereation to the stream
    /// </summary>
    public async Task WriteDelete(Stream stream, HorseMessage message)
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

    private sealed class BoundedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public BoundedReadStream(Stream inner, int length)
        {
            _inner = inner;
            _length = length;
            _start = inner.CanSeek ? inner.Position : 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length)
                return 0;

            int toRead = (int) Math.Min(count, _length - _position);
            int read = _inner.Read(buffer, offset, toRead);
            _position += read;
            return read;
        }

        public override int ReadByte()
        {
            if (_position >= _length)
                return -1;

            int value = _inner.ReadByte();
            if (value >= 0)
                _position++;

            return value;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position >= _length)
                return 0;

            int toRead = (int) Math.Min(count, _length - _position);
            int read = await _inner.ReadAsync(buffer.AsMemory(offset, toRead), cancellationToken);
            _position += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= _length)
                return 0;

            int toRead = (int) Math.Min(buffer.Length, _length - _position);
            int read = await _inner.ReadAsync(buffer[..toRead], cancellationToken);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!_inner.CanSeek)
                throw new NotSupportedException();

            long next = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
            };

            if (next < 0 || next > _length)
                throw new IOException("Attempted to seek outside the bounded record");

            _inner.Position = _start + next;
            _position = next;
            return _position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    #endregion
}
