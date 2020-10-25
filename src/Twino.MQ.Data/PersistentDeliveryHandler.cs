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
    public class PersistentDeliveryHandler : IMessageDeliveryHandler, IPersistentDeliveryHandler
    {
        internal Database Database { get; set; }
        
        /// <summary>
        /// Queue of the delivery handler
        /// </summary>
        public TwinoQueue Queue { get; }

        /// <summary>
        /// Key for delivery handler attribute
        /// </summary>
        public string Key { get; protected set; } = "default";

        /// <summary>
        /// Database Filename
        /// </summary>
        public string DbFilename => Database.File.Filename;

        /// <summary>
        /// Option when to delete messages from disk
        /// </summary>
        public DeleteWhen DeleteWhen { get; }
        
        /// <summary>
        /// Option when to send acknowledge to producer
        /// </summary>
        public ProducerAckDecision ProducerAckDecision { get; }

        /// <summary>
        /// Put back decision when a negative acknowledge received by consumer
        /// </summary>
        public PutBackDecision NegativeAckPutBack { get; set; }

        /// <summary>
        /// Put back decision when acknowledge timed out
        /// </summary>
        public PutBackDecision AckTimeoutPutBack { get; set; }

        #region Init - Destroy

        /// <summary>
        /// Creates new persistent delivery handler
        /// </summary>
        public PersistentDeliveryHandler(TwinoQueue queue,
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
        public virtual async Task Initialize()
        {
            if (Queue.Options.Acknowledge == QueueAckDecision.None)
            {
                if (DeleteWhen == DeleteWhen.AfterAcknowledgeReceived)
                    throw new NotSupportedException("Delete option is AfterAcknowledgeReceived but queue Acknowledge option is None. " +
                                                    "Messages are not deleted from disk with this configuration. " +
                                                    "Please change queue Acknowledge option or DeleteWhen option");

                if (ProducerAckDecision == ProducerAckDecision.AfterConsumerAckReceived)
                    throw new NotSupportedException("Producer Ack option is AfterConsumerAckReceived but queue Acknowledge option is None. " +
                                                    "Messages are not deleted from disk with this configuration. " +
                                                    "Please change queue Acknowledge option or ProducerAckDecision option");
            }

            await Database.Open();
            Queue.OnDestroyed += Destroy;

            var dict = await Database.List();
            if (dict.Count > 0)
            {
                QueueFiller filler = new QueueFiller(Queue);
                PushResult result = filler.FillMessage(dict.Values, true);
                if (result != PushResult.Success)
                    throw new InvalidOperationException($"Cannot fill messages into {Queue.Name} queue : {result}");
            }
        }

        /// <summary>
        /// Removes queue from configuration and deletes all database files
        /// </summary>
        private async void Destroy(TwinoQueue queue)
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
        public virtual Task<Decision> ReceivedFromProducer(TwinoQueue queue, QueueMessage message, MqClient sender)
        {
            DeliveryAcknowledgeDecision save = DeliveryAcknowledgeDecision.None;
            if (ProducerAckDecision == ProducerAckDecision.AfterReceived)
                save = DeliveryAcknowledgeDecision.Always;
            else if (ProducerAckDecision == ProducerAckDecision.AfterSaved)
                save = DeliveryAcknowledgeDecision.IfSaved;

            return Task.FromResult(new Decision(true, true, PutBackDecision.No, save));
        }

        /// <inheritdoc />
        public virtual Task<Decision> BeginSend(TwinoQueue queue, QueueMessage message)
        {
            return Task.FromResult(new Decision(true, true, PutBackDecision.No, DeliveryAcknowledgeDecision.None));
        }

        /// <inheritdoc />
        public virtual Task<Decision> CanConsumerReceive(TwinoQueue queue, QueueMessage message, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public virtual Task<Decision> ConsumerReceived(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public virtual Task<Decision> ConsumerReceiveFailed(TwinoQueue queue, MessageDelivery delivery, MqClient receiver)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public virtual async Task<Decision> EndSend(TwinoQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return new Decision(true, true, PutBackDecision.Start, DeliveryAcknowledgeDecision.None);

            if (DeleteWhen == DeleteWhen.AfterSend)
                await DeleteMessage(message.Message.MessageId);

            return Decision.JustAllow();
        }

        /// <inheritdoc />
        public virtual async Task<Decision> AcknowledgeReceived(TwinoQueue queue, TwinoMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (success && DeleteWhen == DeleteWhen.AfterAcknowledgeReceived)
                await DeleteMessage(delivery.Message.Message.MessageId);
            
            if (ProducerAckDecision == ProducerAckDecision.AfterConsumerAckReceived)
                return new Decision(true, false, PutBackDecision.No, success
                                                                         ? DeliveryAcknowledgeDecision.Always
                                                                         : DeliveryAcknowledgeDecision.Negative);

            return Decision.JustAllow();
        }

        /// <inheritdoc />
        public virtual async Task<Decision> MessageTimedOut(TwinoQueue queue, QueueMessage message)
        {
            await DeleteMessage(message.Message.MessageId);
            return Decision.JustAllow();
        }

        /// <summary>
        /// Deletes message from database
        /// </summary>
        protected virtual async Task DeleteMessage(string id)
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
        public virtual Task<Decision> AcknowledgeTimedOut(TwinoQueue queue, MessageDelivery delivery)
        {
            if (ProducerAckDecision == ProducerAckDecision.AfterConsumerAckReceived)
                return Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.Negative));

            return Task.FromResult(new Decision(true, false, PutBackDecision.Start, DeliveryAcknowledgeDecision.None));
        }

        /// <inheritdoc />
        public virtual Task MessageDequeued(TwinoQueue queue, QueueMessage message)
        {
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public virtual Task<Decision> ExceptionThrown(TwinoQueue queue, QueueMessage message, Exception exception)
        {
            if (ConfigurationFactory.Builder.ErrorAction != null)
                ConfigurationFactory.Builder.ErrorAction(queue, message, exception);
            return Task.FromResult(Decision.JustAllow());
        }

        /// <inheritdoc />
        public virtual Task<bool> SaveMessage(TwinoQueue queue, QueueMessage message)
        {
            return Database.Insert(message.Message);
        }

        #endregion
    }
}