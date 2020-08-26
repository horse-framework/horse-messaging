using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Options;
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
        public bool Add(TwinoQueue queue, string filename)
        {
            QueueOptionsConfiguration queueOptions = queue.Options.ToConfiguration();
            string channelName = queue.Channel.Name;

            ChannelConfiguration channelConfig;
            lock (_optionsLock)
            {
                channelConfig = ConfigurationFactory.Configuration.Channels.FirstOrDefault(x => x.Name == channelName);
                if (channelConfig != null)
                    if (channelConfig.Queues.Any(x => x.QueueId == queue.Id))
                        return false;
            }

            if (channelConfig == null)
            {
                ChannelOptionsConfiguration channelOptions = queue.Channel.Options.ToConfiguration();
                channelConfig = new ChannelConfiguration();
                channelConfig.Configuration = channelOptions;
                channelConfig.Name = queue.Channel.Name;
                channelConfig.Queues = new List<QueueConfiguration>();

                if (ConfigurationFactory.Configuration.Channels == null)
                    ConfigurationFactory.Configuration.Channels = new List<ChannelConfiguration>();

                ConfigurationFactory.Configuration.Channels.Add(channelConfig);
            }

            PersistentDeliveryHandler deliveryHandler = (PersistentDeliveryHandler) queue.DeliveryHandler;

            QueueConfiguration queueConfiguration = new QueueConfiguration();
            queueConfiguration.Configuration = queueOptions;
            queueConfiguration.Channel = channelName;
            queueConfiguration.QueueId = queue.Id;
            queueConfiguration.File = filename;
            queueConfiguration.Queue = queue;
            queueConfiguration.DeleteWhen = Convert.ToInt32(deliveryHandler.DeleteWhen);
            queueConfiguration.ProducerAck = Convert.ToInt32(deliveryHandler.ProducerAckDecision);

            lock (_optionsLock)
                channelConfig.Queues.Add(queueConfiguration);

            return true;
        }

        /// <summary>
        /// Removes queue from configurations
        /// </summary>
        public void Remove(TwinoQueue queue)
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
        public async Task LoadQueues(TwinoMQ server)
        {
            foreach (ChannelConfiguration channelConfiguration in Config.Channels)
            {
                Channel channel = server.FindChannel(channelConfiguration.Name);
                if (channel == null)
                {
                    ChannelOptions options = channelConfiguration.Configuration.ToOptions();
                    channel = server.CreateChannel(channelConfiguration.Name, options);

                    //creation not allowed, skip the channel and it's queues
                    if (channel == null)
                        continue;
                }

                channelConfiguration.Channel = channel;

                if (channelConfiguration.Queues == null)
                    continue;

                foreach (QueueConfiguration queueConfiguration in channelConfiguration.Queues)
                {
                    TwinoQueue queue = channel.FindQueue(queueConfiguration.QueueId);
                    if (queue == null)
                    {
                        queue = await Extensions.CreateQueue(channel,
                                                             queueConfiguration.QueueId,
                                                             (DeleteWhen) queueConfiguration.DeleteWhen,
                                                             (ProducerAckDecision) queueConfiguration.ProducerAck,
                                                             queueConfiguration.Configuration.ToOptions());

                        //queue creation not permitted, skip
                        if (queue == null)
                            continue;
                    }
                    else
                    {
                        PersistentDeliveryHandler deliveryHandler = (PersistentDeliveryHandler) queue.DeliveryHandler;
                        await deliveryHandler.Initialize();
                    }

                    queueConfiguration.Queue = queue;
                }
            }
        }
    }
}