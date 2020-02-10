using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Manages shrink operations for the database
    /// </summary>
    public class ShrinkManager
    {
        #region Fields - Properties

        /// <summary>
        /// Database itself
        /// </summary>
        private readonly Database _database;

        /// <summary>
        /// Default data serializer
        /// </summary>
        private readonly DataMessageSerializer _serializer = new DataMessageSerializer();

        /// <summary>
        /// Source database stream that will be shrunken
        /// </summary>
        private MemoryStream _source;

        /// <summary>
        /// New file stream, shrunken data will be writted to this stream
        /// </summary>
        private FileStream _target;

        /// <summary>
        /// True, if shrink operation is in process
        /// </summary>
        private volatile bool _shrinking;

        /// <summary>
        /// Source stream's ending position
        /// </summary>
        private long _end;

        /// <summary>
        /// Found and deleted files after shrink operation
        /// </summary>
        private HashSet<string> _deletedMessages;

        /// <summary>
        /// Auto shrink timer object
        /// </summary>
        private ThreadTimer _autoShrinkTimer;

        /// <summary>
        /// If true, a new delete request is proceed and database file requires to be shrunken
        /// </summary>
        internal bool ShrinkRequired { get; set; }

        /// <summary>
        /// As a shrink result, deleted messages via shrink operation
        /// </summary>
        public List<string> DeletedMessages { get; private set; } = new List<string>();

        /// <summary>
        /// Buffer for shrink operations
        /// </summary>
        private readonly byte[] _buffer = new byte[10240];

        private ShrinkInfo _info;

        #endregion

        #region Create - Start - Stop - Dispose

        /// <summary>
        /// Creates new shrink manager for the database
        /// </summary>
        public ShrinkManager(Database database)
        {
            _database = database;
        }

        /// <summary>
        /// Starts shrink timer
        /// </summary>
        internal void Start(TimeSpan interval)
        {
            if (_autoShrinkTimer != null)
                Stop();

            _autoShrinkTimer = new ThreadTimer(async () =>
            {
                try
                {
                    if (_shrinking)
                        return;

                    if (ShrinkRequired)
                        await _database.Shrink();
                }
                catch
                {
                }
            }, interval);
            _autoShrinkTimer.Start(ThreadPriority.BelowNormal);
        }

        /// <summary>
        /// Stop shrink timer
        /// </summary>
        internal void Stop()
        {
            if (_autoShrinkTimer != null)
            {
                _autoShrinkTimer.Stop();
                _autoShrinkTimer = null;
            }
        }

        /// <summary>
        /// Disposes source shrink stream
        /// </summary>
        private async Task DisposeSource()
        {
            if (_source == null)
                return;

            _source.Close();
            await _source.DisposeAsync();
            _source = null;
        }

        /// <summary>
        /// Closes target shrink file stream
        /// </summary>
        private async Task CloseTarget()
        {
            if (_target == null)
                return;

            _target.Close();
            await _target.DisposeAsync();
            _target = null;
        }

        #endregion

        #region Shrink

        /// <summary>
        /// Shrinks whole data in database file.
        /// In this operation, database file will be locked 
        /// </summary>
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
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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

        /// <summary>
        /// Shrinks database file from begin to position pointer.
        /// This is a partial shrink and can run almost without any lock and thread-block operations.
        /// </summary>
        public async Task<ShrinkInfo> Shrink(long position, List<string> deletedMessages)
        {
            _info = new ShrinkInfo();

            if (_shrinking)
                return _info;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            _shrinking = true;
            _end = position;
            _deletedMessages = new HashSet<string>(deletedMessages);
            _info.OldSize = _end;

            if (_source != null)
                await DisposeSource();

            await using (FileStream file = new FileStream(_database.File.Filename, FileMode.Open, FileAccess.Read))
            {
                _source = new MemoryStream();
                await CopyStream(file, _source, Convert.ToInt32(_end));
                _source.Position = 0;
            }

            _target = new FileStream(_database.File.Filename + ".shrink", FileMode.Create, FileAccess.Write);

            sw.Stop();
            _info.PreparationDuration = sw.Elapsed;
            sw.Reset();
            sw.Start();

            bool proceed = await ProcessShrink();
            sw.Stop();
            _info.TruncateDuration = sw.Elapsed;
            sw.Reset();

            if (!proceed)
            {
                _shrinking = false;
                return _info;
            }

            sw.Start();
            bool sync = await SyncShrink();
            sw.Stop();
            _info.SyncDuration = sw.Elapsed;

            _shrinking = false;
            ShrinkRequired = false;
            _info.Successful = sync;

            return _info;
        }

        /// <summary>
        /// Reads all shrink area and process messages if they are deleted or not
        /// </summary>
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

        /// <summary>
        /// Merges shrink file with current database file and completes shrink operation
        /// </summary>
        private async Task<bool> SyncShrink()
        {
            await _target.FlushAsync();
            await DisposeSource();

            _info.NewSize = _target.Length;

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
                        int read = await stream.ReadAsync(_buffer, 0, _buffer.Length);
                        if (read == 0)
                            break;

                        await _target.WriteAsync(_buffer, 0, read);
                    }
                }

                await _target.FlushAsync();
                _info.CurrentDatabaseSize = _target.Length;

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

        #endregion

        #region Helpers

        /// <summary>
        /// Copies data from source stream to target stream only first length bytes.
        /// </summary>
        private async Task CopyStream(Stream from, Stream to, int length)
        {
            int left = length;
            while (left > 0)
            {
                int size = left < _buffer.Length ? left : _buffer.Length;
                int read = await from.ReadAsync(_buffer, 0, size);
                left -= read;
                await to.WriteAsync(_buffer, 0, read);
            }
        }

        #endregion
    }
}