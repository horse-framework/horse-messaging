using System;
using System.IO;
using System.Threading.Tasks;
using Twino.MQ.Data.Configuration;
using Twino.MQ.Options;
using Twino.MQ.Queues;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Object for persistent queue extension methods
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Adds persistent queues with default configuration
        /// </summary>
        public static TwinoMQ AddPersistentQueues(this TwinoMQ server)
        {
            return AddPersistentQueues(server, c => { });
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static TwinoMqBuilder AddPersistentQueues(this TwinoMqBuilder builder)
        {
            return AddPersistentQueues(builder, c => { });
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static TwinoMqBuilder AddPersistentQueues(this TwinoMqBuilder builder,
                                                         Action<DataConfigurationBuilder> cfg)
        {
            builder.Server.AddPersistentQueues(cfg);
            return builder;
        }

        /// <summary>
        /// Adds persistent queues with customized configuration
        /// </summary>
        public static TwinoMQ AddPersistentQueues(this TwinoMQ server,
                                                  Action<DataConfigurationBuilder> cfg)
        {
            DataConfigurationBuilder builder = new DataConfigurationBuilder();
            cfg(builder);

            if (builder.GenerateQueueFilename == null)
                builder.GenerateQueueFilename = DefaultQueueDbPath;

            ConfigurationFactory.Initialize(builder);

            return server;
        }

        /// <summary>
        /// Loads all persistent queue messages from databases
        /// </summary>
        public static Task LoadPersistentQueues(this TwinoMQ server)
        {
            if (ConfigurationFactory.Builder == null)
                throw new InvalidOperationException("Before loading queues initialize persistent queues with AddPersistentQueues method");

            return ConfigurationFactory.Manager.LoadQueues(server);
        }

        /// <summary>
        /// Implements persistent message delivery handler
        /// </summary>
        /// <param name="builder">Twino MQ Builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <returns></returns>
        public static TwinoMqBuilder UsePersistentDeliveryHandler(this TwinoMqBuilder builder,
                                                                  DeleteWhen deleteWhen,
                                                                  ProducerAckDecision producerAckDecision)
        {
            builder.Server.DeliveryHandlerFactory = async (dh) =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(dh.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(dh.Queue, databaseOptions, deleteWhen, producerAckDecision);
                await handler.Initialize();
                dh.OnAfterCompleted(AfterDeliveryHandlerCreated);
                return handler;
            };
            return builder;
        }

        /// <summary>
        /// Creates and initializes new persistent delivery handler for the queue
        /// </summary>
        /// <param name="builder">Delivery handler builder</param>
        /// <param name="deleteWhen">Decision when messages are deleted from disk</param>
        /// <param name="producerAckDecision">Decision when producer receives acknowledge</param>
        /// <returns></returns>
        public static async Task<IMessageDeliveryHandler> CreatePersistentDeliveryHandler(this DeliveryHandlerBuilder builder,
                                                                                          DeleteWhen deleteWhen,
                                                                                          ProducerAckDecision producerAckDecision)
        {
            DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
            PersistentDeliveryHandler handler = new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision);
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
        /// Creates new persistent queue
        /// </summary>
        /// <param name="mq">Twino MQ</param>
        /// <param name="queue">Queue Name</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <returns></returns>
        public static Task<TwinoQueue> CreatePersistentQueue(this TwinoMQ mq,
                                                             string queue,
                                                             DeleteWhen deleteWhen,
                                                             ProducerAckDecision producerAckDecision)
        {
            QueueOptions options = QueueOptions.CloneFrom(mq.Options);
            return CreatePersistentQueue(mq, queue, deleteWhen, producerAckDecision, options);
        }

        /// <summary>
        /// Creates new persistent queue
        /// </summary>
        /// <param name="mq">Twino MQ Server</param>
        /// <param name="queue">Queue name</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <param name="optionsAction">Queue Options builder action</param>
        /// <returns></returns>
        public static Task<TwinoQueue> CreatePersistentQueue(this TwinoMQ mq,
                                                             string queue,
                                                             DeleteWhen deleteWhen,
                                                             ProducerAckDecision producerAckDecision,
                                                             Action<QueueOptions> optionsAction)
        {
            QueueOptions options = QueueOptions.CloneFrom(mq.Options);
            optionsAction(options);
            return CreatePersistentQueue(mq, queue, deleteWhen, producerAckDecision, options);
        }

        /// <summary>
        /// Creates new persistent queue
        /// </summary>
        /// <param name="mq">Twino MQ Server</param>
        /// <param name="queueName">Queue name</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="producerAckDecision">Decision, when ack will be sent to producer</param>
        /// <param name="options">Queue Options</param>
        /// <returns></returns>
        public static async Task<TwinoQueue> CreatePersistentQueue(this TwinoMQ mq,
                                                                   string queueName,
                                                                   DeleteWhen deleteWhen,
                                                                   ProducerAckDecision producerAckDecision,
                                                                   QueueOptions options)
        {
            TwinoQueue queue = await CreateQueue(mq, queueName, deleteWhen, producerAckDecision, options);
            IPersistentDeliveryHandler deliveryHandler = (IPersistentDeliveryHandler) queue.DeliveryHandler;
            ConfigurationFactory.Manager.Add(queue, deliveryHandler.DbFilename);
            ConfigurationFactory.Manager.Save();
            return queue;
        }

        /// <summary>
        /// Creates new persistent queue
        /// </summary>
        /// <param name="mq">Twino MQ Server</param>
        /// <param name="queueName">Queue name</param>
        /// <param name="options">Queue Options</param>
        /// <param name="factory">Delivery handler instance creator factory</param>
        /// <returns></returns>
        public static async Task<TwinoQueue> CreatePersistentQueue(this TwinoMQ mq,
                                                                   string queueName,
                                                                   QueueOptions options,
                                                                   Func<DatabaseOptions, IPersistentDeliveryHandler> factory)
        {
            TwinoQueue queue = await CreateQueue(mq, queueName, options, factory);
            IPersistentDeliveryHandler deliveryHandler = (IPersistentDeliveryHandler) queue.DeliveryHandler;
            ConfigurationFactory.Manager.Add(queue, deliveryHandler.DbFilename);
            ConfigurationFactory.Manager.Save();
            return queue;
        }

        /// <summary>
        /// Creates and returns persistent queue
        /// </summary>
        internal static async Task<TwinoQueue> CreateQueue(TwinoMQ mq,
                                                           string queueName,
                                                           DeleteWhen deleteWhen,
                                                           ProducerAckDecision producerAckDecision,
                                                           QueueOptions options)
        {
            return await mq.CreateQueue(queueName, options, async builder =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(builder.Queue, databaseOptions, deleteWhen, producerAckDecision);
                await handler.Initialize();
                return handler;
            });
        }

        /// <summary>
        /// Creates and returns persistent queue
        /// </summary>
        internal static async Task<TwinoQueue> CreateQueue(TwinoMQ mq,
                                                           string queueName,
                                                           QueueOptions options,
                                                           Func<DatabaseOptions, IPersistentDeliveryHandler> factory)
        {
            return await mq.CreateQueue(queueName, options, async builder =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(builder.Queue);
                IPersistentDeliveryHandler handler = factory(databaseOptions);
                await handler.Initialize();
                return handler;
            });
        }

        /// <summary>
        /// Generates full file path for database file of the queue
        /// </summary>
        private static string DefaultQueueDbPath(TwinoQueue queue)
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