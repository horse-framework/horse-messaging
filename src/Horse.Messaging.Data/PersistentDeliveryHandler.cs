using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Handlers;

namespace Horse.Messaging.Data
{
    /// <summary>
    /// Delivery handler for persistent queues
    /// </summary>
    public class PersistentDeliveryHandler : IPersistentDeliveryHandler
    {
        internal Database Database { get; set; }

        /// <summary>
        /// Queue of the delivery handler
        /// </summary>
        public HorseQueue Queue { get; }

        /// <summary>
        /// Database Filename
        /// </summary>
        public string DbFilename => Database.File.Filename;

        /// <summary>
        /// Option when to delete messages from disk
        /// </summary>
        public DeleteWhen DeleteWhen { get; }

        /// <summary>
        /// Option when to send commit to producer
        /// </summary>
        public CommitWhen CommitWhen { get; }

        /// <summary>
        /// Put back decision when a negative acknowledge received by consumer.
        /// Default is End with no delay.
        /// </summary>
        public PutBackDecision NegativeAckPutBack { get; set; } = PutBackDecision.Regular;

        /// <summary>
        /// Put back decision when acknowledge timed out.
        /// Default is End with no delay.
        /// </summary>
        public PutBackDecision AckTimeoutPutBack { get; set; } = PutBackDecision.Regular;

        /// <summary>
        /// Redelivery service for the queue
        /// </summary>
        public RedeliveryService RedeliveryService { get; private set; }

        /// <summary>
        /// True If redelivery is used for the queue
        /// </summary>
        public bool UseRedelivery { get; }

        #region Init - Destroy

        /// <summary>
        /// Creates new persistent delivery handler
        /// </summary>
        public PersistentDeliveryHandler(HorseQueue queue,
                                         DatabaseOptions options,
                                         DeleteWhen deleteWhen,
                                         CommitWhen commitWhen,
                                         bool useRedelivery = false)
        {
            Queue = queue;
            DeleteWhen = deleteWhen;
            CommitWhen = commitWhen;
            Database = new Database(options);
            UseRedelivery = useRedelivery;
        }

        /// <summary>
        /// Initializes queue, opens database files and fills messages into the queue
        /// </summary>
        public virtual async Task Initialize()
        {
            if (Queue.Options.Acknowledge == QueueAckDecision.None)
            {
                if (DeleteWhen == DeleteWhen.AfterAcknowledge)
                    throw new NotSupportedException("Delete option is AfterAcknowledgeReceived but queue Acknowledge option is None. " +
                                                    "Messages are not deleted from disk with this configuration. " +
                                                    "Please change queue Acknowledge option or DeleteWhen option");

                if (CommitWhen == CommitWhen.AfterAcknowledge)
                    throw new NotSupportedException("Producer Ack option is AfterConsumerAckReceived but queue Acknowledge option is None. " +
                                                    "Messages are not deleted from disk with this configuration. " +
                                                    "Please change queue Acknowledge option or ProducerAckDecision option");
            }

            await Database.Open();
            Queue.OnDestroyed += Destroy;

            List<KeyValuePair<string, int>> deliveries = null;
            if (UseRedelivery)
            {
                RedeliveryService = new RedeliveryService(Database.File.Filename + ".delivery");
                await RedeliveryService.Load();
                deliveries = RedeliveryService.GetDeliveries();
            }

            var dict = await Database.List();
            if (dict.Count > 0)
            {
                QueueFiller filler = new QueueFiller(Queue);
                PushResult result = filler.FillMessage(dict.Values,
                                                       true,
                                                       qm =>
                                                       {
                                                           if (!UseRedelivery ||
                                                               deliveries == null ||
                                                               deliveries.Count == 0 ||
                                                               string.IsNullOrEmpty(qm.Message.MessageId))
                                                               return;

                                                           var kv = deliveries.FirstOrDefault(x => x.Key == qm.Message.MessageId);
                                                           if (kv.Value > 0)
                                                               qm.DeliveryCount = kv.Value;
                                                       });

                if (result != PushResult.Success)
                    throw new InvalidOperationException($"Cannot fill messages into {Queue.Name} queue : {result}");
            }
        }

        /// <summary>
        /// Removes queue from configuration and deletes all database files
        /// </summary>
        private async void Destroy(HorseQueue queue)
        {
            if (UseRedelivery)
            {
                await RedeliveryService.Close();
                RedeliveryService.Delete();
            }

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
        public virtual async Task<Decision> ReceivedFromProducer(HorseQueue queue, QueueMessage message, MessagingClient sender)
        {
            if (CommitWhen == CommitWhen.AfterSaved)
            {
                bool saved = await SaveMessage(queue, message);
                if (!saved)
                {
                    return Decision.InterruptFlow(false, DecisionTransmission.Failed);
                }

                return Decision.TransmitToProducer(DecisionTransmission.Commit);
            }

            return Decision.SaveMessage(CommitWhen == CommitWhen.AfterReceived
                                            ? DecisionTransmission.Commit
                                            : DecisionTransmission.None);
        }

        /// <inheritdoc />
        public virtual async Task<Decision> BeginSend(HorseQueue queue, QueueMessage message)
        {
            if (UseRedelivery)
            {
                message.DeliveryCount++;
                await RedeliveryService.Set(message.Message.MessageId, message.DeliveryCount);

                if (message.DeliveryCount > 1)
                    message.Message.SetOrAddHeader(HorseHeaders.DELIVERY, message.DeliveryCount.ToString());
            }

            return Decision.NoveNext();
        }

        /// <inheritdoc />
        public virtual Task<Decision> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <inheritdoc />
        public virtual Task<Decision> ConsumerReceived(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <inheritdoc />
        public virtual Task<Decision> ConsumerReceiveFailed(HorseQueue queue, MessageDelivery delivery, MessagingClient receiver)
        {
            return Task.FromResult(Decision.NoveNext());
        }

        /// <inheritdoc />
        public virtual async Task<Decision> EndSend(HorseQueue queue, QueueMessage message)
        {
            if (message.SendCount == 0)
                return Decision.PutBackMessage(false);

            if (DeleteWhen == DeleteWhen.AfterSend)
            {
                await DeleteMessage(message.Message.MessageId);
                return Decision.DeleteMessage();
            }

            return Decision.NoveNext();
        }

        /// <inheritdoc />
        public virtual async Task<Decision> AcknowledgeReceived(HorseQueue queue, HorseMessage acknowledgeMessage, MessageDelivery delivery, bool success)
        {
            if (success)
            {
                if (DeleteWhen == DeleteWhen.AfterAcknowledge)
                {
                    await DeleteMessage(delivery.Message.Message.MessageId);
                    return Decision.DeleteMessage(CommitWhen == CommitWhen.AfterAcknowledge
                                                      ? DecisionTransmission.Commit
                                                      : DecisionTransmission.None);
                }

                if (CommitWhen == CommitWhen.AfterAcknowledge)
                    return Decision.TransmitToProducer(DecisionTransmission.Commit);
            }
            else
            {
                if (CommitWhen == CommitWhen.AfterAcknowledge)
                {
                    if (NegativeAckPutBack == PutBackDecision.No)
                        return Decision.TransmitToProducer(DecisionTransmission.Failed);

                    return Decision.PutBackMessage(NegativeAckPutBack == PutBackDecision.Regular, DecisionTransmission.Failed);
                }
            }

            return Decision.NoveNext();
        }

        /// <inheritdoc />
        public virtual async Task<Decision> MessageTimedOut(HorseQueue queue, QueueMessage message)
        {
            await DeleteMessage(message.Message.MessageId);
            return Decision.DeleteMessage();
        }

        /// <summary>
        /// Deletes message from database
        /// </summary>
        protected virtual async Task DeleteMessage(string id)
        {
            if (UseRedelivery)
                await RedeliveryService.Remove(id);

            for (int i = 0; i < 3; i++)
            {
                bool deleted = await Database.Delete(id);
                if (deleted)
                    return;

                await Task.Delay(3);
            }
        }

        /// <inheritdoc />
        public virtual Task<Decision> AcknowledgeTimedOut(HorseQueue queue, MessageDelivery delivery)
        {
            if (AckTimeoutPutBack == PutBackDecision.No)
            {
                QueueMessage queueMessage = delivery.Message;

                if (!queueMessage.IsRemoved)
                {
                    _ = DeleteMessage(queueMessage.Message.MessageId);
                    return Task.FromResult(Decision.DeleteMessage(CommitWhen == CommitWhen.AfterAcknowledge
                                                                      ? DecisionTransmission.Failed
                                                                      : DecisionTransmission.None));
                }

                if (CommitWhen == CommitWhen.AfterAcknowledge)
                    return Task.FromResult(Decision.TransmitToProducer(DecisionTransmission.Failed));
            }
            else
            {
                return Task.FromResult(Decision.PutBackMessage(AckTimeoutPutBack == PutBackDecision.Regular,
                                                               CommitWhen == CommitWhen.AfterAcknowledge
                                                                   ? DecisionTransmission.Failed
                                                                   : DecisionTransmission.None));
            }

            return Task.FromResult(Decision.NoveNext());
        }

        /// <inheritdoc />
        public virtual Task MessageDequeued(HorseQueue queue, QueueMessage message)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task<Decision> ExceptionThrown(HorseQueue queue, QueueMessage message, Exception exception)
        {
            if (ConfigurationFactory.Builder.ErrorAction != null)
                ConfigurationFactory.Builder.ErrorAction(queue, message, exception);

            return Task.FromResult(Decision.NoveNext());
        }

        /// <inheritdoc />
        public virtual Task<bool> SaveMessage(HorseQueue queue, QueueMessage message)
        {
            return Database.Insert(message.Message);
        }

        #endregion
    }
}