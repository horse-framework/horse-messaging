using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            QueueOptionsConfiguration queueOptions = queue.Options.ToConfiguration();

            QueueConfiguration queueConfiguration = new QueueConfiguration();
            queueConfiguration.Configuration = queueOptions;
            queueConfiguration.Name = queue.Name;
            queueConfiguration.File = filename;
            queueConfiguration.Queue = queue;

            if (queue.DeliveryHandler is IPersistentDeliveryHandler deliveryHandler)
            {
                queueConfiguration.DeliveryHandler = deliveryHandler.Key;
                queueConfiguration.DeleteWhen = Convert.ToInt32(deliveryHandler.DeleteWhen);
                queueConfiguration.ProducerAck = Convert.ToInt32(deliveryHandler.ProducerAckDecision);
            }

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
        public async Task LoadQueues(HorseMq server)
        {
            foreach (QueueConfiguration queueConfiguration in Config.Queues)
            {
                HorseQueue queue = server.FindQueue(queueConfiguration.Name);
                if (queue == null)
                {
                    if (server.DeliveryHandlerFactory != null)
                        queue = await server.CreateQueue(queueConfiguration.Name,
                                                         queueConfiguration.Configuration.ToOptions(),
                                                         async builder =>
                                                         {
                                                             builder.DeliveryHandlerHeader = queueConfiguration.DeliveryHandler;
                                                             IMessageDeliveryHandler handler = await server.DeliveryHandlerFactory(builder);
                                                             builder.OnAfterCompleted(b => { }); //don't trigger created events, it's already created and reloading
                                                             return handler;
                                                         });
                    else
                        queue = await Extensions.CreateQueue(server,
                                                             queueConfiguration.Name,
                                                             (DeleteWhen) queueConfiguration.DeleteWhen,
                                                             (ProducerAckDecision) queueConfiguration.ProducerAck,
                                                             queueConfiguration.Configuration.ToOptions());

                    //queue creation not permitted, skip
                    if (queue == null)
                        continue;
                }
                else
                {
                    if (queue.DeliveryHandler is IPersistentDeliveryHandler deliveryHandler)
                        await deliveryHandler.Initialize();
                }

                queueConfiguration.Queue = queue;
            }
        }
    }
}