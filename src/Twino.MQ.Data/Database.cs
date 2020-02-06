using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    public class Database
    {
        #region Properties

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly DataMessageSerializer _serializer = new DataMessageSerializer();
        private bool _shrinkRequired;

        private readonly List<string> _deletedMessages = new List<string>();
        private readonly Dictionary<string, TmqMessage> _messages = new Dictionary<string, TmqMessage>(StringComparer.InvariantCultureIgnoreCase);

        public DatabaseFile File { get; }
        public DatabaseOptions Options { get; }

        #endregion

        #region Open - Close

        public Database(DatabaseOptions options)
        {
            Options = options;
            File = new DatabaseFile(options.Filename);
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public async Task Load()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Management

        public async Task<bool> RemoveDatabase()
        {
            await File.Close();
            return await File.Delete();
        }

        public async Task<bool> Shrink()
        {
            ShrinkManager manager = new ShrinkManager(this);
            Stream stream = File.GetStream();
            long position = stream.Position;

            List<string> msgs;
            lock (_deletedMessages)
                msgs = new List<string>(_deletedMessages);

            bool success = await manager.Shrink(position, msgs);

            //sync deleted messages array
            if (success)
            {
                if (manager.DeletedMessages.Count > 0)
                    lock (_deletedMessages)
                        _deletedMessages.RemoveAll(x => manager.DeletedMessages.Contains(x));
            }

            _shrinkRequired = false;
            return success;
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
                if (_messages.ContainsKey(message.MessageId))
                    return false;

                _messages.Add(message.MessageId, message);
                Stream stream = File.GetStream();
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
                Stream stream = File.GetStream();
                await _serializer.WriteDelete(stream, message);
                _messages.Remove(message);

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

        public async Task<Dictionary<string, TmqMessage>> List()
        {
            Dictionary<string, TmqMessage> messages;
            await WaitForLock();
            try
            {
                messages = new Dictionary<string, TmqMessage>(_messages);
            }
            finally
            {
                ReleaseLock();
            }

            return messages;
        }

        #endregion
    }
}