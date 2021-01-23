using System;
using Horse.Mq.Queues;

namespace Horse.Mq.Data.Configuration
{
    /// <summary>
    /// Data configuration builder for persistent queues
    /// </summary>
    public class DataConfigurationBuilder
    {
        #region Fields - Properties

        private bool _instantFlush = false;
        private bool _autoFlush = true;
        private bool _createBackup = true;
        private bool _autoShrink = true;
        private TimeSpan _flushInteval = TimeSpan.FromMilliseconds(250);
        private TimeSpan _shrinkInteval = TimeSpan.FromMinutes(15);

        internal string ConfigFile { get; private set; } = "data/config.json";
        internal Func<HorseQueue, string> GenerateQueueFilename { get; set; }
        
        internal Action<HorseQueue, QueueMessage, Exception> ErrorAction { get; set; }

        #endregion

        /// <summary>
        /// Changes default configuration file for persistent queues
        /// </summary>
        public DataConfigurationBuilder SetConfigFile(string fullpath)
        {
            ConfigFile = fullpath;
            return this;
        }

        /// <summary>
        /// If true, messages are saved with high performance.
        /// But it does not guarantee to not lose any message is saved in last a few milliseconds if server shutdown unexpectedly.
        /// This operation disabled instant flush.
        /// </summary>
        public DataConfigurationBuilder UseAutoFlush()
        {
            return UseAutoFlush(TimeSpan.FromMilliseconds(250));
        }

        /// <summary>
        /// If true, messages are saved with high performance.
        /// But it does not guarantee to not lose any message is saved in last a few milliseconds (interval milliseconds), if server shutdown unexpectedly.
        /// This operation disabled instant flush.
        /// </summary>
        public DataConfigurationBuilder UseAutoFlush(TimeSpan interval)
        {
            _instantFlush = false;
            _autoFlush = true;
            _flushInteval = interval;
            return this;
        }

        /// <summary>
        /// If true, flush operation is applied immediately when message is saved.
        /// Setting it true can reduce your save performance,
        /// but guarantees to not lose any message is saved in last a few milliseconds, if server shutdown unexpectedly.
        /// This operation disabled auto flush. 
        /// </summary>
        public DataConfigurationBuilder UseInstantFlush()
        {
            _autoFlush = false;
            _flushInteval = TimeSpan.FromSeconds(60);
            _instantFlush = true;
            return this;
        }

        /// <summary>
        /// If true, each shrink keeps last backup file. Default is true
        /// </summary>
        public DataConfigurationBuilder KeepLastBackup(bool value = true)
        {
            _createBackup = value;
            return this;
        }

        /// <summary>
        /// Sets auto shrink option for queue.
        /// Auto shrink, reduces database file size periodically and creates new backup.
        /// It's highly recommended. Because delete message operation does not reduce file size, it marks messages as deleted.
        /// Shrink operation removes messages from size permanently.
        /// For this overload, 15 min default interval value will be used.
        /// </summary>
        public DataConfigurationBuilder SetAutoShrink(bool value)
        {
            return SetAutoShrink(value, TimeSpan.FromMinutes(15));
        }

        /// <summary>
        /// Sets auto shrink option for queue.
        /// Auto shrink, reduces database file size periodically and creates new backup.
        /// It's highly recommended. Because delete message operation does not reduce file size, it marks messages as deleted.
        /// Shrink operation removes messages from size permanently.
        /// </summary>
        /// <param name="value">True is enabled</param>
        /// <param name="interval">Shrink interval. Default is 15 mins. Recommended minimum 1 min, maximum 24 hours.</param>
        public DataConfigurationBuilder SetAutoShrink(bool value, TimeSpan interval)
        {
            _autoShrink = value;
            _shrinkInteval = interval;
            return this;
        }

        /// <summary>
        /// Sets database fullpath generater action. Executed for each queue.
        /// </summary>
        public DataConfigurationBuilder SetPhysicalPath(Func<HorseQueue, string> func)
        {
            GenerateQueueFilename = func;
            return this;
        }

        /// <summary>
        /// Creates new DatabaseOptions using predefined options
        /// </summary>
        internal DatabaseOptions CreateOptions(HorseQueue queue)
        {
            return new DatabaseOptions
                   {
                       Filename = GenerateQueueFilename(queue),
                       AutoFlush = _autoFlush,
                       AutoShrink = _autoShrink,
                       FlushInterval = _flushInteval,
                       InstantFlush = _instantFlush,
                       ShrinkInterval = _shrinkInteval,
                       CreateBackupOnShrink = _createBackup
                   };
        }
    }
}