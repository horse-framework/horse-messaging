using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Data.Configuration
{
    internal class DataConfigurationManager
    {
        private readonly object _optionsLock = new object();

        private DataConfiguration Config => ConfigurationFactory.Configuration;

        /// <summary>
        /// Loads configurations
        /// </summary>
        public DataConfiguration Load(string fullpath)
        {
            if (!File.Exists(fullpath))
            {
                var c = DataConfiguration.Empty();
                string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(c);

                string dir = FindDirectoryIfFile(ConfigurationFactory.Builder.ConfigFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(ConfigurationFactory.Builder.ConfigFile, serialized);
                return c;
            }

            string json = File.ReadAllText(fullpath);
            DataConfiguration configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<DataConfiguration>(json);
            return configuration;
        }

        private string FindDirectoryIfFile(string fullpath)
        {
            return fullpath.Substring(0, fullpath.LastIndexOf('/'));
        }

        /// <summary>
        /// Saves current configurations
        /// </summary>
        public void Save()
        {
            try
            {
                string serialized;
                lock (_optionsLock)
                    serialized = Newtonsoft.Json.JsonConvert.SerializeObject(Config);

                string dir = FindDirectoryIfFile(ConfigurationFactory.Builder.ConfigFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(ConfigurationFactory.Builder.ConfigFile, serialized);
            }
            catch (Exception e)
            {
                if (ConfigurationFactory.Builder.ErrorAction != null)
                    ConfigurationFactory.Builder.ErrorAction(null, null, e);
            }
        }

        /// <summary>
        /// Adds new queue into configurations
        /// </summary>
        public bool Add(HorseQueue queue, string filename)
        {
            PersistentQueueManager manager = queue.Manager as PersistentQueueManager;
            if (manager == null)
                return false;

            lock (_optionsLock)
                if (Config.Queues.Any(x => x.Name == queue.Name))
                    return false;

            QueueOptionsConfiguration queueOptions = queue.Options.ToConfiguration();

            QueueConfiguration queueConfiguration = new QueueConfiguration();
            queueConfiguration.Configuration = queueOptions;
            queueConfiguration.Name = queue.Name;
            queueConfiguration.File = filename;
            queueConfiguration.Queue = queue;

            lock (_optionsLock)
                Config.Queues.Add(queueConfiguration);

            return true;
        }

        /// <summary>
        /// Removes queue from configurations
        /// </summary>
        public void Remove(HorseQueue queue)
        {
            lock (_optionsLock)
            {
                QueueConfiguration queueConfiguration = Config.Queues.FirstOrDefault(x => x.Name == queue.Name);

                if (queueConfiguration != null)
                    Config.Queues.Remove(queueConfiguration);
            }
        }

        /// <summary>
        /// Loads messages of queues in configuration
        /// </summary>
        public async Task LoadQueues(HorseRider rider)
        {
            foreach (QueueConfiguration queueConfiguration in Config.Queues)
            {
                HorseQueue queue = rider.Queue.Find(queueConfiguration.Name);
                if (queue == null)
                {
                    queue = await rider.Queue.Create(queueConfiguration.Name,
                                                     queueConfiguration.Configuration.ToOptions(),
                                                     null, false, false, null,
                                                     "PERSISTENT");

                    //queue creation not permitted, skip
                    if (queue == null)
                        continue;
                }
                else
                {
                    await queue.Manager.Initialize();
                }

                queueConfiguration.Queue = queue;
            }
        }
    }
}