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
        private readonly ShrinkManager _shrinkManager;

        private readonly List<string> _deletedMessages = new List<string>();
        private readonly Dictionary<string, TmqMessage> _messages = new Dictionary<string, TmqMessage>(StringComparer.InvariantCultureIgnoreCase);

        public DatabaseFile File { get; }
        public DatabaseOptions Options { get; }

        #endregion

        #region Open - Close

        public Database(DatabaseOptions options)
        {
            Options = options;
            File = new DatabaseFile(options);
            _shrinkManager = new ShrinkManager(this);
        }

        public async Task Open()
        {
            await File.Open();
            await Load();

            if (_deletedMessages.Count > 0)
                await _shrinkManager.FullShrink(_messages, _deletedMessages);

            if (Options.AutoShrink)
                await _shrinkManager.Start(Options.ShrinkInterval);
        }

        private async Task Load()
        {
            await WaitForLock();
            try
            {
                Stream stream = File.GetStream();
                stream.Seek(0, SeekOrigin.Begin);
                while (stream.Position < stream.Length)
                {
                    DataMessage message = await _serializer.Read(stream);
                    switch (message.Type)
                    {
                        case DataType.Insert:
                            _messages.Add(message.Id, message.Message);
                            break;

                        case DataType.Delete:
                            _deletedMessages.Add(message.Id);
                            break;
                    }
                }
            }
            finally
            {
                ReleaseLock();
            }
        }

        public async Task Close()
        {
            await _shrinkManager.Stop();
            await File.Close();
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
            Stream stream = File.GetStream();
            long position = stream.Position;

            List<string> msgs;
            lock (_deletedMessages)
                msgs = new List<string>(_deletedMessages);

            bool success = await _shrinkManager.Shrink(position, msgs);

            //sync deleted messages array
            if (success)
            {
                if (_shrinkManager.DeletedMessages.Count > 0)
                    lock (_deletedMessages)
                        _deletedMessages.RemoveAll(x => _shrinkManager.DeletedMessages.Contains(x));
            }

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
            if (string.IsNullOrEmpty(message.MessageId))
                return false;

            await WaitForLock();
            try
            {
                if (_messages.ContainsKey(message.MessageId))
                    return false;

                _messages.Add(message.MessageId, message);
                Stream stream = File.GetStream();
                await _serializer.Write(stream, message);

                if (Options.InstantFlush)
                    await stream.FlushAsync();
                else
                    File.FlushRequired = true;

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
            if (string.IsNullOrEmpty(message.MessageId))
                return false;

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
                _deletedMessages.Add(message);

                if (!_shrinkManager.ShrinkRequired)
                    _shrinkManager.ShrinkRequired = true;

                if (Options.InstantFlush)
                    await stream.FlushAsync();
                else
                    File.FlushRequired = true;

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