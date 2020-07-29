using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Database file backup options
    /// </summary>
    public enum BackupOption
    {
        /// <summary>
        /// Creates new backup file as copy of database file
        /// </summary>
        Copy,

        /// <summary>
        /// Renames database file
        /// </summary>
        Move
    }

    /// <summary>
    /// Database file and operations belong it
    /// </summary>
    public class DatabaseFile
    {
        /// <summary>
        /// File name. May be relative or absolute full path.
        /// </summary>
        public string Filename { get; }

        /// <summary>
        /// File stream object of the database file
        /// </summary>
        private FileStream _file;

        /// <summary>
        /// File auto flush timer
        /// </summary>
        private Timer _flushTimer;

        /// <summary>
        /// Database object
        /// </summary>
        private readonly Database _database;

        /// <summary>
        /// Managed by database and file objects.
        /// If true, auto flush timer will call flush method of the file
        /// </summary>
        internal bool FlushRequired { get; set; }

        /// <summary>
        /// Creates new database file
        /// </summary>
        public DatabaseFile(Database database)
        {
            _database = database;
            Filename = database.Options.Filename;
        }

        /// <summary>
        /// Gets current open file stream
        /// </summary>
        public Stream GetStream()
        {
            return _file;
        }

        /// <summary>
        /// Flushes file stream.
        /// If dblock is true, operation will block all insert, delete and shrink operations til end
        /// </summary>
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

        /// <summary>
        /// Opens database file and seeks to end
        /// </summary>
        public async Task Open()
        {
            if (_file != null)
                return;

            _file = new FileStream(Filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (_file.Length > 0)
                _file.Seek(0, SeekOrigin.End);

            if (_database.Options.AutoFlush)
                await StartFlushTimer();
        }

        /// <summary>
        /// Starts flush timer for database file stream
        /// </summary>
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

        /// <summary>
        /// Closes database file stream.
        /// If dblock is true, operation will block all insert, delete and shrink operations til end
        /// </summary>
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

        /// <summary>
        /// Deletes database file
        /// </summary>
        public async Task<bool> Delete()
        {
            if (_file != null)
                await Close();

            try
            {
                File.Delete(Filename);
                File.Delete(Filename + ".backup");
                File.Delete(Filename + ".shrink");
                return true;
            }
            catch (Exception e)
            {
                _database.TriggerError(ErrorHint.DeleteDatabaseFile, e);
                return false;
            }
        }

        /// <summary>
        /// Backups database file.
        /// If dblock is true, operation will block all insert, delete and shrink operations til end
        /// </summary>
        public async Task<bool> Backup(BackupOption option, bool dblock = true)
        {
            if (_file == null)
                return false;

            try
            {
                await Close(dblock);

                if (option == BackupOption.Move)
                    File.Move(Filename, Filename + ".backup", true);
                else
                    File.Copy(Filename, Filename + ".backup", true);

                return true;
            }
            catch (Exception e)
            {
                _database.TriggerError(ErrorHint.Backup, e);
                return false;
            }
        }
    }
}