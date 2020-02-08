using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    public class ShrinkManager
    {
        private readonly Database _database;
        private readonly DataMessageSerializer _serializer = new DataMessageSerializer();

        private MemoryStream _source;
        private FileStream _target;

        private volatile bool _shrinking;
        private long _end;
        private List<string> _deletedMessages;
        private Timer _autoShrinkTimer;

        internal bool ShrinkRequired { get; set; }

        public List<string> DeletedMessages { get; private set; } = new List<string>();

        public ShrinkManager(Database database)
        {
            _database = database;
        }

        internal async Task Start(TimeSpan interval)
        {
            if (_autoShrinkTimer != null)
                await Stop();

            _autoShrinkTimer = new Timer(async s =>
            {
                try
                {
                    if (ShrinkRequired)
                        await _database.Shrink();
                }
                catch
                {
                }
            }, "", interval, interval);
        }

        internal async Task Stop()
        {
            if (_autoShrinkTimer != null)
            {
                await _autoShrinkTimer.DisposeAsync();
                _autoShrinkTimer = null;
            }
        }

        private async Task DisposeSource()
        {
            if (_source == null)
                return;
            
            _source.Close();
            await _source.DisposeAsync();
            _source = null;
        }

        private async Task CloseTarget()
        {
            if (_target == null)
                return;
            
            _target.Close();
            await _target.DisposeAsync();
            _target = null;
        }

        public async Task<bool> FullShrink(Dictionary<string, TmqMessage> messages, List<string> deletedItems)
        {
            _shrinking = true;
            bool backup = false;
            await using MemoryStream ms = new MemoryStream();

            await _database.WaitForLock();
            try
            {
                //shrink whole file
                foreach (string deletedItem in deletedItems)
                    messages.Remove(deletedItem);

                deletedItems.Clear();

                foreach (KeyValuePair<string, TmqMessage> kv in messages)
                    await _serializer.Write(ms, kv.Value);

                backup = await _database.File.Backup(BackupOption.Move, false);
                if (!backup)
                    return false;

                ms.Position = 0;
                await _database.File.Open();
                await ms.CopyToAsync(_database.File.GetStream());
                await _database.File.Flush(false);

                if (!_database.Options.CreateBackupOnShrink)
                {
                    try
                    {
                        File.Delete(_database.File.Filename + ".backup");
                    }
                    catch
                    {
                    }
                }

                return true;
            }
            catch
            {
                //reverse
                if (backup)
                {
                    try
                    {
                        File.Delete(_database.File.Filename);
                        File.Move(_database.File.Filename + ".backup", _database.File.Filename);
                    }
                    catch
                    {
                    }
                }

                return false;
            }
            finally
            {
                _shrinking = false;
                _database.ReleaseLock();
            }
        }

        public async Task<bool> Shrink(long position, List<string> deletedMessages)
        {
            if (_shrinking)
                return false;

            _shrinking = true;
            _end = position;
            _deletedMessages = deletedMessages;
            
            if (_source != null)
                await DisposeSource();

            await using (FileStream file = new FileStream(_database.File.Filename, FileMode.Open, FileAccess.Read))
            {
                _source = new MemoryStream();
                await CopyStream(file, _source, Convert.ToInt32(_end));
                _source.Position = 0;
            }

            _target = new FileStream(_database.File.Filename + ".shrink", FileMode.Create, FileAccess.Write);

            bool proceed = await ProcessShrink();
            if (!proceed)
            {
                _shrinking = false;
                return false;
            }

            bool sync = await SyncShrink();

            _shrinking = false;
            ShrinkRequired = false;
            return sync;
        }

        private async Task<bool> ProcessShrink()
        {
            List<string> deletedMessages = new List<string>();
            await using MemoryStream ms = new MemoryStream(Convert.ToInt32(_end));

            while (_source.Position < _source.Length)
            {
                DataType type = _serializer.ReadType(_source);
                string id = await _serializer.ReadId(_source);
                bool deleted = _deletedMessages.Contains(id);

                switch (type)
                {
                    case DataType.Insert:
                        int length = await _serializer.ReadLength(_source);
                        if (deleted)
                        {
                            _source.Seek(length, SeekOrigin.Current);
                            deletedMessages.Add(id);
                        }
                        else
                        {
                            ms.WriteByte((byte) type);
                            await _serializer.WriteId(ms, id);
                            bool written = await _serializer.WriteContent(length, _source, ms);
                            if (!written)
                                return false;
                        }

                        break;

                    case DataType.Delete:
                        if (!deleted)
                            await _serializer.WriteDelete(ms, id);
                        else
                            deletedMessages.Add(id);

                        break;
                }
            }

            ms.Position = 0;
            await ms.CopyToAsync(_target);

            DeletedMessages = deletedMessages;
            return true;
        }

        private async Task CopyStream(Stream from, Stream to, int length)
        {
            byte[] buffer = new byte[10240];
            int left = length;
            while (left > 0)
            {
                int size = left < buffer.Length ? left : buffer.Length;
                int read = await from.ReadAsync(buffer, 0, size);
                left -= read;
                await to.WriteAsync(buffer, 0, read);
            }
        }

        private async Task<bool> SyncShrink()
        {
            byte[] buffer = new byte[10240];

            await _target.FlushAsync();
            await DisposeSource();
            await _database.WaitForLock();
            try
            {
                //write left data from file to shrink file before swap-chain
                Stream stream = _database.File.GetStream();
                if (stream.Length > _end)
                {
                    stream.Seek(_end, SeekOrigin.Begin);
                    while (stream.Position < stream.Length)
                    {
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;

                        await _target.WriteAsync(buffer, 0, read);
                    }
                }

                await _target.FlushAsync();
                await CloseTarget();
                await _database.File.Close(false);

                File.Move(_database.File.Filename, _database.File.Filename + ".backup", true);
                File.Move(_database.File.Filename + ".shrink", _database.File.Filename, true);

                if (!_database.Options.CreateBackupOnShrink)
                {
                    try
                    {
                        File.Delete(_database.File.Filename + ".backup");
                    }
                    catch
                    {
                    }
                }

                await _database.File.Open();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _database.ReleaseLock();
            }
        }
    }
}