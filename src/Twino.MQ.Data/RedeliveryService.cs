using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Redelivery service keeps messages in queue how many times they are sent to consumers.
    /// That services keeps the data even when server is restarted.
    /// </summary>
    public class RedeliveryService
    {
        private readonly Dictionary<string, int> _redeliveries = new Dictionary<string, int>();

        private readonly string _fullpath;
        private FileStream _stream;
        private readonly SemaphoreSlim _slim = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Creates new redelivery service
        /// </summary>
        public RedeliveryService(string fullpath)
        {
            _fullpath = fullpath;
        }

        /// <summary>
        /// Gets all deliveries.
        /// Usually used to sync queue message delivery values while initializing.
        /// </summary>
        public List<KeyValuePair<string, int>> GetDeliveries()
        {
            List<KeyValuePair<string, int>> list;
            lock (_redeliveries)
                list = new List<KeyValuePair<string, int>>(_redeliveries);

            return list;
        }

        /// <summary>
        /// Adds a message to redeliveries list or increases delivery count
        /// </summary>
        public Task Set(string messageId, int deliveryCount)
        {
            lock (_redeliveries)
            {
                if (_redeliveries.ContainsKey(messageId))
                    _redeliveries[messageId] = deliveryCount;
                else
                    _redeliveries.Add(messageId, deliveryCount);
            }

            return Save();
        }

        /// <summary>
        /// Removes a message from redeliveries list
        /// </summary>
        public Task Remove(string messageId)
        {
            lock (_redeliveries)
                _redeliveries.Remove(messageId);

            return Save();
        }

        /// <summary>
        /// Clears all redeliveries
        /// </summary>
        /// <returns></returns>
        public Task Clear()
        {
            lock (_redeliveries)
                _redeliveries.Clear();

            return Save();
        }

        /// <summary>
        /// Loads redeliveries and initializes service
        /// </summary>
        public async Task Load()
        {
            await _slim.WaitAsync();
            try
            {
                _stream = new FileStream(_fullpath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                StreamReader reader = new StreamReader(_stream, Encoding.ASCII);
                while (!reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    string[] split = line.Trim().Split('\t');

                    lock (_redeliveries)
                        if (_redeliveries.ContainsKey(split[0]))
                            _redeliveries[split[0]] = Convert.ToInt32(split[1]);
                        else
                            _redeliveries.Add(split[0], Convert.ToInt32(split[1]));
                }
            }
            finally
            {
                _slim.Release();
            }
        }

        /// <summary>
        /// Saves current redelivery data to disk
        /// </summary>
        public async Task Save()
        {
            await using MemoryStream ms = new MemoryStream();
            await using StreamWriter writer = new StreamWriter(ms, Encoding.ASCII);

            lock (_redeliveries)
            {
                foreach (var pair in _redeliveries)
                    writer.Write(pair.Key + "\t" + pair.Value + "\n");
            }

            ms.Position = 0;
            await _slim.WaitAsync();
            try
            {
                _stream.Position = 0;
                _stream.SetLength(ms.Length);
                await ms.CopyToAsync(_stream);
                await _stream.FlushAsync();
            }
            finally
            {
                _slim.Release();
            }
        }

        /// <summary>
        /// Closes redelivery file and releases all resources
        /// </summary>
        public async Task Close()
        {
            await _slim.WaitAsync();
            try
            {
                await _stream.FlushAsync();
                _stream.Close();
                await _stream.DisposeAsync();
                _stream = null;
            }
            finally
            {
                _slim.Release();
            }
        }

        /// <summary>
        /// Deletes the message
        /// </summary>
        public void Delete()
        {
            try
            {
                File.Delete(_fullpath);
            }
            catch
            {
            }
        }
    }
}