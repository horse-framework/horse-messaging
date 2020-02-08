using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.MQ.Data
{
    public enum BackupOption
    {
        Copy,
        Move
    }

    public class DatabaseFile
    {
        public string Filename { get; }

        private FileStream _file;
        private Timer _flushTimer;
        private readonly Database _database;

        internal bool FlushRequired { get; set; }

        public DatabaseFile(Database database)
        {
            _database = database;
            Filename = database.Options.Filename;
        }

        public Stream GetStream()
        {
            return _file;
        }

        public async Task Flush(bool dblock = true)
        {
            if (_file == null)
                return;

            if (dblock)
                await _database.WaitForLock();

            try
            {
                await _file.FlushAsync();
            }
            finally
            {
                if (dblock)
                    _database.ReleaseLock();
            }
        }

        public async Task Open()
        {
            if (_file != null)
                return;

            _file = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            _file.Seek(_file.Length, SeekOrigin.Begin);

            if (_database.Options.AutoFlush)
                await StartFlushTimer();
        }

        internal async Task StartFlushTimer()
        {
            if (_flushTimer != null)
            {
                await _flushTimer.DisposeAsync();
                _flushTimer = null;
            }

            _flushTimer = new Timer(async s =>
            {
                try
                {
                    if (_file != null)
                    {
                        if (FlushRequired)
                        {
                            FlushRequired = false;
                            await Flush();
                        }
                    }
                }
                catch
                {
                }
            }, "", TimeSpan.FromSeconds(2), _database.Options.FlushInterval);
        }

        public async Task Close(bool dblock = true)
        {
            if (_file == null)
                return;

            if (dblock)
                await _database.WaitForLock();

            try
            {
                await _file.FlushAsync();
                await _file.DisposeAsync();
                _file = null;

                if (_flushTimer != null)
                {
                    await _flushTimer.DisposeAsync();
                    _flushTimer = null;
                }
            }
            finally
            {
                if (dblock)
                    _database.ReleaseLock();
            }
        }

        public async Task<bool> Delete()
        {
            if (_file != null)
                await Close();

            try
            {
                File.Delete(Filename);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> Backup(BackupOption option, bool dblock = true)
        {
            if (_file == null)
                return false;

            try
            {
                await Close(dblock);

                if (option == BackupOption.Move)
                    File.Move(Filename, Filename + ".backup");
                else
                    File.Copy(Filename, Filename + ".backup");

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}