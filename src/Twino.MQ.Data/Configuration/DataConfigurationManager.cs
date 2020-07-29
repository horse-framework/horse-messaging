using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Data.Configuration
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
                File.WriteAllText(ConfigurationFactory.Builder.ConfigFile, serialized);
                return c;
            }

            string json = File.ReadAllText(fullpath);
            DataConfiguration configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<DataConfiguration>(json);
            return configuration;
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
                File.WriteAllText(ConfigurationFactory.Builder.ConfigFile, serialized);
            }
            catch (Exception e)
            {
                if (ConfigurationFactory.Builder.ErrorAction != null)
                    ConfigurationFactory.Builder.ErrorAction(e);
            }
        }

        /// <summary>
        /// Adds new queue into configurations
        /// </summary>
        public void Add(ChannelQueue queue, string filename)
        {
            QueueOptionsConfiguration queueOptions = queue.Options.ToConfiguration();
            string channelName = queue.Channel.Name;

            ChannelConfiguration channelConfig;
            lock (_optionsLock)
                channelConfig = ConfigurationFactory.Configuration.Channels.FirstOrDefault(x => x.Name == channelName);

            if (channelConfig == null)
            {
                ChannelOptionsConfiguration channelOptions = queue.Channel.Options.ToConfiguration();
                channelConfig = new ChannelConfiguration();
                channelConfig.Configuration = channelOptions;
                channelConfig.Name = queue.Channel.Name;
                channelConfig.Queues = new List<QueueConfiguration>();
            }

            QueueConfiguration queueConfiguration = new QueueConfiguration();
            queueConfiguration.Configuration = queueOptions;
            queueConfiguration.Channel = channelName;
            queueConfiguration.QueueId = queue.Id;
            queueConfiguration.File = filename;

            lock (_optionsLock)
                channelConfig.Queues.Add(queueConfiguration);
        }

        /// <summary>
        /// Removes queue from configurations
        /// </summary>
        public void Remove(ChannelQueue queue)
        {
            string channelName = queue.Channel.Name;

            ChannelConfiguration channelConfiguration;
            lock (_optionsLock)
                channelConfiguration = Config.Channels.FirstOrDefault(x => x.Name == channelName);

            //already removed
            if (channelConfiguration == null)
                return;

            lock (_optionsLock)
            {
                QueueConfiguration queueConfiguration = channelConfiguration.Queues.FirstOrDefault(x => x.QueueId == queue.Id);

                //if last queue in channel is removing, remove channel too 
                if (queueConfiguration != null && channelConfiguration.Queues.Count == 1)
                    channelConfiguration.Queues.Remove(queueConfiguration);
                else
                    Config.Channels.Remove(channelConfiguration);
            }
        }

        /// <summary>
        /// Loads messages of queues in configuration
        /// </summary>
        public Task LoadQueues(MqServer server)
        {
            throw new NotImplementedException();
        }
    }
}