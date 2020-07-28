using System;
using System.IO;
using System.Threading.Tasks;
using Twino.MQ.Delivery;
using Twino.MQ.Options;
using Twino.MQ.Queues;

namespace Twino.MQ.Data
{
    public static class Extensions
    {
        /// <summary>
        /// Adds persistent queues with configuration
        /// </summary>
        public static MqServer AddPersistentQueues(this MqServer server,
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
        public static Task LoadPersistentQueues(this MqServer server)
        {
            if (ConfigurationFactory.Builder == null)
                throw new InvalidOperationException("Before loading queues initialize persistent queues with AddPersistentQueues method");

            return ConfigurationFactory.Manager.LoadQueues();
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="channel">The channel queue will be created in</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="acknowledgeDecision">Decision, when ack will be sent to producer</param>
        /// <param name="exception">Exception handler action for delivery handler</param>
        /// <returns></returns>
        public static Task<ChannelQueue> CreatePersistentQueue(this Channel channel,
                                                               ushort queueId,
                                                               DeleteWhen deleteWhen,
                                                               DeliveryAcknowledgeDecision acknowledgeDecision,
                                                               Action<ChannelQueue, QueueMessage, Exception> exception = null)
        {
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            return CreatePersistentQueue(channel, queueId, deleteWhen, acknowledgeDecision, options, exception);
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="channel">The channel queue will be created in</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="acknowledgeDecision">Decision, when ack will be sent to producer</param>
        /// <param name="optionsAction">Channel Queue Options builder action</param>
        /// <param name="exception">Exception handler action for delivery handler</param>
        /// <returns></returns>
        public static Task<ChannelQueue> CreatePersistentQueue(this Channel channel,
                                                               ushort queueId,
                                                               DeleteWhen deleteWhen,
                                                               DeliveryAcknowledgeDecision acknowledgeDecision,
                                                               Action<ChannelQueueOptions> optionsAction,
                                                               Action<ChannelQueue, QueueMessage, Exception> exception = null)
        {
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(channel.Options);
            optionsAction(options);
            return CreatePersistentQueue(channel, queueId, deleteWhen, acknowledgeDecision, options, exception);
        }

        /// <summary>
        /// Creates new persistent queue in the channel
        /// </summary>
        /// <param name="channel">The channel queue will be created in</param>
        /// <param name="queueId">Queue Id</param>
        /// <param name="deleteWhen">Decision, when messages will be removed from disk</param>
        /// <param name="acknowledgeDecision">Decision, when ack will be sent to producer</param>
        /// <param name="options">Channel Queue Options</param>
        /// <param name="exception">Exception handler action for delivery handler</param>
        /// <returns></returns>
        public static async Task<ChannelQueue> CreatePersistentQueue(this Channel channel,
                                                                     ushort queueId,
                                                                     DeleteWhen deleteWhen,
                                                                     DeliveryAcknowledgeDecision acknowledgeDecision,
                                                                     ChannelQueueOptions options,
                                                                     Action<ChannelQueue, QueueMessage, Exception> exception = null)
        {
            ChannelQueue queue = await channel.CreateQueue(queueId, options, async q =>
            {
                DatabaseOptions databaseOptions = ConfigurationFactory.Builder.CreateOptions(q);
                PersistentDeliveryHandler handler = new PersistentDeliveryHandler(q, databaseOptions, deleteWhen, acknowledgeDecision, exception);
                await handler.Initialize();
                q.OnDestroyed += handler.Destroy;
                return handler;
            });

            ConfigurationFactory.Manager.Add(queue);
            ConfigurationFactory.Manager.Save();
            return queue;
        }

        /// <summary>
        /// Generates full file path for database file of the queue
        /// </summary>
        private static string DefaultQueueDbPath(ChannelQueue queue)
        {
            string dir = "data/" + queue.Channel.Name;
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return dir + "/" + queue.Id + ".tdb";
            }
            catch
            {
                return "data-" + queue.Channel.Name + "-" + queue.Id + ".tdb";
            }
        }
    }
}