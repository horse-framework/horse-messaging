using System;
using System.IO;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

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
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="commitWhen">Decision when producer receives commit</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <param name="ackTimeoutPutback">Putback decision when ack message isn't received</param>
        /// <param name="nackPutback">Putback decision when negative ack is received</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg,
                                                                          DeleteWhen deleteWhen,
                                                                          CommitWhen commitWhen,
                                                                          bool useRedelivery = false,
                                                                          PutBackDecision ackTimeoutPutback = PutBackDecision.Regular,
                                                                          PutBackDecision nackPutback = PutBackDecision.Regular)
        {
            return UsePersistentQueues(cfg, _ => { }, deleteWhen, commitWhen, useRedelivery, ackTimeoutPutback, nackPutback);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="dataConfigurator">Persistent data store configurator</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="commitWhen">Decision when producer receives commit</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <param name="ackTimeoutPutback">Putback decision when ack message isn't received</param>
        /// <param name="nackPutback">Putback decision when negative ack is received</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentQueues(this HorseQueueConfigurator cfg,
                                                                 Action<DataConfigurationBuilder> dataConfigurator,
                                                                 DeleteWhen deleteWhen,
                                                                 CommitWhen commitWhen,
                                                                 bool useRedelivery = false,
                                                                 PutBackDecision ackTimeoutPutback = PutBackDecision.Regular,
                                                                 PutBackDecision nackPutback = PutBackDecision.Regular)
        {
            DataConfigurationBuilder dataConfigurationBuilder = new DataConfigurationBuilder();
            dataConfigurator(dataConfigurationBuilder);

            if (dataConfigurationBuilder.GenerateQueueFilename == null)
                dataConfigurationBuilder.GenerateQueueFilename = DefaultQueueDbPath;

            ConfigurationFactory.Initialize(dataConfigurationBuilder);

            cfg.Rider.Queue.QueueManagerFactories.Add("PERSISTENT", async dh =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(dh.Queue);
                PersistentQueueManager manager = new PersistentQueueManager(dh.Queue,
                                                                            databaseOptions,
                                                                            commitWhen,
                                                                            deleteWhen,
                                                                            nackPutback,
                                                                            ackTimeoutPutback,
                                                                            useRedelivery);

                await manager.Initialize();
                return manager;
            });

            if (!cfg.Rider.Queue.QueueManagerFactories.ContainsKey("DEFAULT"))
                cfg.Rider.Queue.QueueManagerFactories.Add("DEFAULT", cfg.Rider.Queue.QueueManagerFactories["PERSISTENT"]);

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