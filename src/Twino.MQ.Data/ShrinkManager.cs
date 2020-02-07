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

        private FileStream _file;
        private FileStream _shrink;

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
            await _autoShrinkTimer.DisposeAsync();
            _autoShrinkTimer = null;
        }

        private async Task CloseFile()
        {
            _file.Close();
            await _file.DisposeAsync();
            _file = null;
        }

        private async Task CloseShrink()
        {
            _shrink.Close();
            await _shrink.DisposeAsync();
            _shrink = null;
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

                backup = await _database.File.Backup(BackupOption.Move);
                if (!backup)
                    return false;

                ms.Position = 0;
                await _database.File.Open();
                await ms.CopyToAsync(_database.File.GetStream());
                await _database.File.Flush();

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

            _file = new FileStream(_database.File.Filename, FileMode.Open, FileAccess.Read);
            _shrink = new FileStream(_database.File.Filename + ".shrink", FileMode.Create, FileAccess.Write);

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

            while (_file.Position < _end)
            {
                DataType type = _serializer.ReadType(_file);
                string id = await _serializer.ReadId(_file);
                bool deleted = _deletedMessages.Contains(id);

                switch (type)
                {
                    case DataType.Insert:
                        int length = await _serializer.ReadLength(_file);
                        if (deleted)
                        {
                            _file.Seek(length, SeekOrigin.Current);
                            deletedMessages.Add(id);
                        }
                        else
                        {
                            ms.WriteByte((byte) type);
                            await _serializer.WriteId(ms, id);
                            bool written = await _serializer.WriteContent(length, _file, ms);
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
            await ms.CopyToAsync(_shrink);

            DeletedMessages = deletedMessages;
            return true;
        }

        private async Task<bool> SyncShrink()
        {
            byte[] buffer = new byte[10240];

            await _shrink.FlushAsync();
            await CloseFile();
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

                        await _shrink.WriteAsync(buffer, 0, read);
                    }
                }

                await CloseShrink();
                await _database.File.Close();

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