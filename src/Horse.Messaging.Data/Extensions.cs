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
        /// Adds persistent queues with default configuration
        /// </summary>
        public static HorseRider AddPersistentQueues(this HorseRider server)
        {
            return AddPersistentQueues(server, _ => { });
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static HorseQueueConfigurator AddPersistentQueues(this HorseQueueConfigurator builder)
        {
            return AddPersistentQueues(builder, _ => { });
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static HorseQueueConfigurator AddPersistentQueues(this HorseQueueConfigurator configurator,
                                                                 Action<DataConfigurationBuilder> cfg)
        {
            configurator.Rider.AddPersistentQueues(cfg);
            return configurator;
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static HorseRider AddPersistentQueues(this HorseRider server,
                                                     Action<DataConfigurationBuilder> cfg)
        {
            DataConfigurationBuilder builder = new DataConfigurationBuilder();
            cfg(builder);

            if (builder.GenerateQueueFilename == null)
                builder.GenerateQueueFilename = DefaultQueueDbPath;

            ConfigurationFactory.Initialize(builder);

            server.Queue.DeliveryHandlerFactories.Add("PERSISTENT", async b =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision);
                await handler.Initialize();
                return handler;
            });

            server.Queue.DeliveryHandlerFactories.Add("PERSISTENT_RELOAD", async b =>
            {
                Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> func = server.Queue.DeliveryHandlerFactories["PERSISTENT"];
                IMessageDeliveryHandler handler = await func(b);
                b.OnAfterCompleted(_ => { }); //don't trigger created events, it's already created and reloading
                return handler;
            });

            return server;
        }

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
            cfg.Rider.Queue.DeliveryHandlerFactories.Add("PERSISTENT", async dh =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(dh.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(dh.Queue, databaseOptions,
                                                                                  deleteWhen,
                                                                                  producerAckDecision,
                                                                                  useRedelivery,
                                                                                  dh.DeliveryHandlerHeader);

                handler.AckTimeoutPutBack = ackTimeoutPutback;
                handler.NegativeAckPutBack = nackPutback;

                await handler.Initialize();
                dh.OnAfterCompleted(AfterDeliveryHandlerCreated);
                return handler;
            });

            return cfg;
        }

        /// <summary>
        /// Creates and initializes new persistent delivery handler for the queue
        /// </summary>
        /// <param name="builder">Delivery handler builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <param name="useRedelivery">True if want to keep redelivery data and send to consumers with message headers</param>
        /// <param name="key">Definition key for delivery handler. You can manage with that key, how the queue will be reloaded.</param>
        /// <param name="ackTimeoutPutback">Putback decision when ack message isn't received</param>
        /// <param name="nackPutback">Putback decision when negative ack is received</param>
        /// <returns></returns>
        public static async Task<IMessageDeliveryHandler> CreatePersistentDeliveryHandler(this DeliveryHandlerBuilder builder,
                                                                                          DeleteWhen deleteWhen,
                                                                                          ProducerAckDecision producerAckDecision,
                                                                                          bool useRedelivery = false,
                                                                                          PutBackDecision ackTimeoutPutback = PutBackDecision.End,
                                                                                          PutBackDecision nackPutback = PutBackDecision.End,
                                                                                          string key = "default")
        {
            DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
            PersistentDeliveryHandler handler = new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision, useRedelivery, key);
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