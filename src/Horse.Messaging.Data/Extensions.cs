using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Data
{
    /// <summary>
    /// Object for persistent queue extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg)
        {
            return UsePersistentQueues(cfg, null, null, false);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg, bool useRedelivery)
        {
            return UsePersistentQueues(cfg, null, null, useRedelivery);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="managerName">Queue manager name</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg, string managerName, bool useRedelivery = false)
        {
            return UsePersistentQueues(cfg, managerName, null, null, useRedelivery);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="dataConfigurator">Persistent data store configurator</param>
        /// <param name="queueConfig">Queue configurator action right after queue manager is assigned to the queue</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg,
            Action<DataConfigurationBuilder> dataConfigurator,
            Action<HorseQueue> queueConfig = null,
            bool useRedelivery = false)
        {
            return UsePersistentQueues(cfg, null, dataConfigurator, queueConfig, useRedelivery);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="managerName">Queue manager name</param>
        /// <param name="dataConfigurator">Persistent data store configurator</param>
        /// <param name="queueConfig">Queue configurator action right after queue manager is assigned to the queue</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg,
            string managerName,
            Action<DataConfigurationBuilder> dataConfigurator = null,
            Action<HorseQueue> queueConfig = null,
            bool useRedelivery = false)
        {
            DataConfigurationBuilder dataConfigurationBuilder = new DataConfigurationBuilder();
            dataConfigurator?.Invoke(dataConfigurationBuilder);

            if (dataConfigurationBuilder.GenerateQueueFilename == null)
                dataConfigurationBuilder.GenerateQueueFilename = DefaultQueueDbPath;

            ConfigurationFactory.Initialize(dataConfigurationBuilder);

            if (string.IsNullOrEmpty(managerName))
                managerName = "Persistent";

            cfg.Rider.Queue.QueueManagerFactories.Add(managerName, dh =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(dh.Queue);
                PersistentQueueManager manager = new PersistentQueueManager(dh.Queue, databaseOptions, useRedelivery);
                dh.Queue.Manager = manager;
                queueConfig?.Invoke(dh.Queue);
                return Task.FromResult<IHorseQueueManager>(manager);
            });

            if (!cfg.Rider.Queue.QueueManagerFactories.ContainsKey("Default"))
                cfg.Rider.Queue.QueueManagerFactories.Add("Default", cfg.Rider.Queue.QueueManagerFactories["Persistent"]);

            ConfigurationFactory.Manager.LoadQueues(cfg.Rider).GetAwaiter().GetResult();

            return cfg;
        }

        /// <summary>
        /// Generates full file path for database file of the queue
        /// </summary>
        private static string DefaultQueueDbPath(HorseQueue queue)
        {
            string dir = "data";
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return dir + "/" + queue.Name + ".tdb";
            }
            catch
            {
                return "data-" + queue.Name + ".tdb";
            }
        }
    }
}