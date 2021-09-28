using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Server;
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
        /// Loads all persistent queue messages from databases
        /// </summary>
        public static Task LoadPersistentQueues(this HorseRider server)
        {
            if (ConfigurationFactory.Builder == null)
                throw new InvalidOperationException("Before loading queues initialize persistent queues with AddPersistentQueues method");

            return ConfigurationFactory.Manager.LoadQueues(server);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <param name="ackTimeoutPutback">Putback decision when ack message isn't received</param>
        /// <param name="nackPutback">Putback decision when negative ack is received</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentDeliveryHandler(this HorseQueueConfigurator cfg,
                                                                          DeleteWhen deleteWhen,
                                                                          ProducerAckDecision producerAckDecision,
                                                                          bool useRedelivery = false,
                                                                          PutBackDecision ackTimeoutPutback = PutBackDecision.End,
                                                                          PutBackDecision nackPutback = PutBackDecision.End)
        {
            return UsePersistentDeliveryHandler(cfg, _ => { }, deleteWhen, producerAckDecision, useRedelivery, ackTimeoutPutback, nackPutback);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="cfg">Horse Clietn configurator Builder</param>
        /// <param name="dataConfigurator">Persistent data store configurator</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <param name="ackTimeoutPutback">Putback decision when ack message isn't received</param>
        /// <param name="nackPutback">Putback decision when negative ack is received</param>
        /// <returns></returns>
        public static HorseQueueConfigurator UsePersistentDeliveryHandler(this HorseQueueConfigurator cfg,
                                                                          Action<DataConfigurationBuilder> dataConfigurator,
                                                                          DeleteWhen deleteWhen,
                                                                          ProducerAckDecision producerAckDecision,
                                                                          bool useRedelivery = false,
                                                                          PutBackDecision ackTimeoutPutback = PutBackDecision.End,
                                                                          PutBackDecision nackPutback = PutBackDecision.End)
        {
            DataConfigurationBuilder dataConfigurationBuilder = new DataConfigurationBuilder();
            dataConfigurator(dataConfigurationBuilder);

            if (dataConfigurationBuilder.GenerateQueueFilename == null)
                dataConfigurationBuilder.GenerateQueueFilename = DefaultQueueDbPath;

            ConfigurationFactory.Initialize(dataConfigurationBuilder);

            cfg.Rider.Queue.DeliveryHandlerFactories.Add("PERSISTENT", async dh =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(dh.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(dh.Queue, databaseOptions,
                                                                                  deleteWhen,
                                                                                  producerAckDecision,
                                                                                  useRedelivery);

                handler.AckTimeoutPutBack = ackTimeoutPutback;
                handler.NegativeAckPutBack = nackPutback;

                await handler.Initialize();
                dh.OnAfterCompleted(AfterDeliveryHandlerCreated);
                return handler;
            });

            cfg.Rider.Queue.DeliveryHandlerFactories.Add("PERSISTENT_RELOAD", async b =>
            {
                Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> func = cfg.Rider.Queue.DeliveryHandlerFactories["PERSISTENT"];
                IMessageDeliveryHandler handler = await func(b);
                b.OnAfterCompleted(_ => { }); //don't trigger created events, it's already created and reloading
                return handler;
            });

            if (!cfg.Rider.Queue.DeliveryHandlerFactories.ContainsKey("DEFAULT"))
                cfg.Rider.Queue.DeliveryHandlerFactories.Add("DEFAULT", cfg.Rider.Queue.DeliveryHandlerFactories["PERSISTENT"]);

            return cfg;
        }

        /// <summary>
        /// Creates and initializes new persistent delivery handler for the queue
        /// </summary>
        /// <param name="builder">Delivery handler builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <param name="ackTimeoutPutback">Putback decision when ack message isn't received</param>
        /// <param name="nackPutback">Putback decision when negative ack is received</param>
        /// <returns></returns>
        public static async Task<IMessageDeliveryHandler> CreatePersistentDeliveryHandler(this DeliveryHandlerBuilder builder,
                                                                                          DeleteWhen deleteWhen,
                                                                                          ProducerAckDecision producerAckDecision,
                                                                                          bool useRedelivery = false,
                                                                                          PutBackDecision ackTimeoutPutback = PutBackDecision.End,
                                                                                          PutBackDecision nackPutback = PutBackDecision.End)
        {
            DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
            PersistentDeliveryHandler handler =
                new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision, useRedelivery);
            handler.AckTimeoutPutBack = ackTimeoutPutback;
            handler.NegativeAckPutBack = nackPutback;
            await handler.Initialize();
            builder.OnAfterCompleted(AfterDeliveryHandlerCreated);
            return handler;
        }

        /// <summary>
        /// Creates and initializes new persistent delivery handler for the queue
        /// </summary>
        /// <param name="builder">Delivery handler builder</param>
        /// <param name="factory">Creates new persistent delivery handler instance</param>
        /// <returns></returns>
        public static async Task<IMessageDeliveryHandler> CreatePersistentDeliveryHandler(this DeliveryHandlerBuilder builder,
                                                                                          Func<DatabaseOptions, IPersistentDeliveryHandler> factory)
        {
            DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
            IPersistentDeliveryHandler handler = factory(databaseOptions);
            await handler.Initialize();
            builder.OnAfterCompleted(AfterDeliveryHandlerCreated);
            return handler;
        }

        private static void AfterDeliveryHandlerCreated(DeliveryHandlerBuilder builder)
        {
            IPersistentDeliveryHandler persistentHandler = builder.Queue.DeliveryHandler as IPersistentDeliveryHandler;
            if (persistentHandler == null)
                return;

            bool added = ConfigurationFactory.Manager.Add(builder.Queue, persistentHandler.DbFilename);
            if (added)
                ConfigurationFactory.Manager.Save();
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