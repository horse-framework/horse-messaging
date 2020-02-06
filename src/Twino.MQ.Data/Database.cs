using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Twino.Db;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    public class Database
    {
        #region Properties

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly DatabaseFile _file;
        private readonly DataMessageSerializer _serializer = new DataMessageSerializer();
        private bool _shrinkRequired = false;

        public DatabaseFile File { get; }

        #endregion

        #region Open - Close

        public Database(string filename)
        {
            File = new DatabaseFile(filename);
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Management

        public void RemoveDatabase()
        {
            throw new NotImplementedException();
        }

        public async Task Shrink()
        {
            ShrinkManager manager = new ShrinkManager(this);
            
            throw new NotImplementedException();
        }

        #endregion

        #region Lock

        public async Task WaitForLock()
        {
            await _semaphore.WaitAsync();
        }

        public void ReleaseLock()
        {
            _semaphore.Release();
        }

        #endregion

        #region Insert - Delete - List

        public async Task<bool> Insert(TmqMessage message)
        {
            await WaitForLock();
            try
            {
                Stream stream = _file.GetStream();
                await _serializer.Write(stream, message);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseLock();
            }
        }

        public async Task<bool> Delete(TmqMessage message)
        {
            return await Delete(message.MessageId);
        }

        public async Task<bool> Delete(string message)
        {
            await WaitForLock();
            try
            {
                Stream stream = _file.GetStream();
                await _serializer.WriteDelete(stream, message);
                
                if (!_shrinkRequired)
                    _shrinkRequired = true;
                
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseLock();
            }
        }

        public async Task<IEnumerable<TmqMessage>> Load()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}