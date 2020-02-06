using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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

        public List<string> DeletedMessages { get; private set; } = new List<string>();

        public ShrinkManager(Database database)
        {
            _database = database;
        }

        private async Task Close()
        {
            _file.Close();
            await _file.DisposeAsync();
            _file = null;

            _shrink.Close();
            await _shrink.DisposeAsync();
            _shrink = null;
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
                        {
                            ms.WriteByte((byte) type);
                            await _serializer.WriteId(ms, id);
                        }
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
            await _shrink.FlushAsync();
            await Close();
            await _database.WaitForLock();
            try
            {
                await _database.File.Close();

                File.Move(_database.File.Filename, _database.File.Filename + ".backup", true);
                File.Move(_database.File.Filename + ".shrink", _database.File.Filename, true);

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