using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.MQ.Data;

namespace Twino.Db
{
    public class ShrinkManager
    {
        private readonly Database _database;

        private FileStream _file;
        private FileStream _shrink;

        private volatile bool _shrinking = false;
        private long _shrinkEnd;
        private List<string> _deletedMessages;

        public ShrinkManager(Database database)
        {
            _database = database;
        }

        public async Task BeginShrink(long position, List<string> deletedMessages)
        {
            _shrinking = true;
            _shrinkEnd = position;
            _deletedMessages = deletedMessages;

            _file = new FileStream(_database.File.Filename, FileMode.Open, FileAccess.Read);
            _shrink = new FileStream(_database.File.Filename + ".shrink", FileMode.Create, FileAccess.Write);


            throw new NotImplementedException();
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

        public async Task SyncShrink()
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
            }
            finally
            {
                _database.ReleaseLock();
            }
        }
    }
}