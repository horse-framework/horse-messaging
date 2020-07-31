using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Data.Configuration;
using Twino.MQ.Delivery;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Delivery handler for persistent queues
    /// </summary>
    public class PersistentDeliveryHandler : IMessageDeliveryHandler
    {
        internal Database Database { get; set; }
        internal ChannelQueue Queue { get; }
        internal DeleteWhen DeleteWhen { get; }
        internal ProducerAckDecision ProducerAckDecision { get; }

        #region Init - Destroy

        /// <summary>
        /// Creates new persistent delivery handler
        /// </summary>
        public PersistentDeliveryHandler(ChannelQueue queue,
                                         DatabaseOptions options,
                                         DeleteWhen deleteWhen,
                                         ProducerAckDecision producerAckDecision)
        {
            Queue = queue;
            DeleteWhen = deleteWhen;
            ProducerAckDecision = producerAckDecision;
            Database = new Database(options);
        }

        /// <summary>
        /// Initializes queue, opens database files and fills messages into the queue
        /// </summary>
        public async Task Initialize()
        {
            await Database.Open();
            Queue.OnDestroyed += Destroy;

            var dict = await Database.List();
            if (dict.Count > 0)
            {
                QueueFiller filler = new QueueFiller(Queue);
                PushResult result = filler.FillMessage(dict.Values, true);
                if (result != PushResult.Success)
                    throw new InvalidOperationException($"Cannot fill messages into {Queue.Id} queue in {Queue.Channel.Name} : {result}");
            }
        }

        /// <summary>
        /// Removes queue from configuration and deletes all database files
        /// </summary>
        private async void Destroy(ChannelQueue queue)
        {
            try
            {
                ConfigurationFactory.Manager.Remove(queue);
                ConfigurationFactory.Manager.Save();

                await Database.Close();
                for (int i = 0; i < 5; i++)
                {
                    bool deleted = await Database.File.Delete();
                    if (deleted)
                        break;

                    await Task.Delay(3);
                }
            }
            catch (Exception e)
            {
                if (ConfigurationFactory.Builder.ErrorAction != null)
                    ConfigurationFactory.Builder.ErrorAction(queue, null, e);
            }
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public Task<Decision> ReceivedFromProducer(ChannelQueue queue, QueueMessage message, MqClient sender)
        {
            DeliveryAcknowledgeDecision save = DeliveryAcknowledgeDecision.None;
            if (ProducerAckDecision == ProducerAckDecision.AfterReceived)
                save = DeliveryAcknowledgeDecision.IfSaved;

            return Task.FromResult(new Decision(true, true, PutBackDecision.No, save));
        }

        /// <inheritdoc />
        public Task<Decision> BeginSend(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(new Decision(true, true, PutBackDecision.No, DeliveryAcknowledgeDecision.None));
        }

        /// <inheritdoc />
        public Task<Decision> CanConsumerReceive(ChannelQueue queue, QueueMessage message, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public Task<Decision> ConsumerReceived(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public Task<Decision> ConsumerReceiveFailed(ChannelQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public async Task<Decision> EndSend(ChannelQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return new Decision(true, true, PutBackDecision.Start, DeliveryAcknowledgeDecision.None);
            if (DeleteWhen == DeleteWhen.AfterSend)

                await DeleteMessage(message.Message.MessageId);
            return Decision.JustAllow();
        }

        /// <inheritdoc />
        public async Task<Decision> AcknowledgeReceived(ChannelQueue queue, TmqMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (DeleteWhen == DeleteWhen.AfterAcknowledgeReceived)
                await DeleteMessage(delivery.Message.Message.MessageId);

            if (ProducerAckDecision == ProducerAckDecision.AfterConsumerAckReceived)
                return new Decision(true, false, PutBackDecision.No, success
                                                                         ? DeliveryAcknowledgeDecision.Always
                                                                         : DeliveryAcknowledgeDecision.Negative);

            return Decision.JustAllow();
        }

        /// <inheritdoc />
        public async Task<Decision> MessageTimedOut(ChannelQueue queue, QueueMessage message)
        {
            await DeleteMessage(message.Message.MessageId);
            return Decision.JustAllow();
        }

        private async Task DeleteMessage(string id)
        {
            for (int i = 0; i < 3; i++)
            {
                bool deleted = await Database.Delete(id);
                if (deleted)
                    return;

                await Task.Delay(3);
            }
        }

        /// <inheritdoc />
        public Task<Decision> AcknowledgeTimedOut(ChannelQueue queue, MessageDelivery delivery)
        {
            if (ProducerAckDecision == ProducerAckDecision.AfterConsumerAckReceived)
                return Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.Negative));

            return Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <inheritdoc />
        public Task MessageDequeued(ChannelQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public Task<Decision> ExceptionThrown(ChannelQueue queue, QueueMessage message, Exception exception)
        {
            if (ConfigurationFactory.Builder.ErrorAction != null)
                ConfigurationFactory.Builder.ErrorAction(queue, message, exception);
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public Task<bool> SaveMessage(ChannelQueue queue, QueueMessage message)
        {
            return Database.Insert(message.Message);
        }

        #endregion
    }
}