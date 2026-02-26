using System;
using System.IO;
using System.Threading.Channels;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Stores;

public class StoreFile
{
    public string Filename { get; set; }
    public int PartitionIndex { get; set; }
    public long BeginOffset { get; set; }
    public bool IsOpen { get; private set; }

    private long _offset = 0;
    private FileStream _writerStream;
    private FileStream _readerStream;

    private readonly Channel<HorseMessage> _channel = Channel.CreateBounded<HorseMessage>(
        new BoundedChannelOptions(10000)
        {
            SingleWriter = true,
            SingleReader = true
        });

    public void Run()
    {
        throw new NotImplementedException();
    }

    public bool Write(HorseMessage message)
    {
        return _channel.Writer.TryWrite(message);
    }

    private void WriteBinaryMessage(BinaryWriter writer, HorseMessage message, long time, long offset)
    {
        writer.Write(offset);
        writer.Write(time);
        byte[] content = HorseProtocolWriter.Create(message);
        writer.Write(content.Length);
        writer.Write(content);
    }

    public void CommitOffset(long offset)
    {
        throw new NotImplementedException();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        if (string.IsNullOrWhiteSpace(Filename))
            throw new InvalidOperationException("Store filename is not set.");

        _writerStream?.Dispose();
        _writerStream = null;

        _readerStream?.Dispose();
        _readerStream = null;

        try
        {
            // Reader and writer must share the same file without locking each other.
            _writerStream = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            _readerStream = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);

            _writerStream.Seek(0, SeekOrigin.End);
            _readerStream.Seek(0, SeekOrigin.Begin);

            _offset = _writerStream.Position;
            IsOpen = true;
        }
        catch
        {
            _readerStream?.Dispose();
            _readerStream = null;

            _writerStream?.Dispose();
            _writerStream = null;

            IsOpen = false;
            throw;
        }
    }

    public void Close()
    {
        throw new NotImplementedException();
    }

    public void Delete()
    {
        throw new NotImplementedException();
    }
}