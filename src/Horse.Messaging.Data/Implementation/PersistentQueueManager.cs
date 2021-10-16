using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Data.Implementation
{
    public class PersistentQueueManager : IHorseQueueManager
    {
        /// <summary>
        /// Redelivery service for the queue
        /// </summary>
        public RedeliveryService RedeliveryService { get; private set; }

        /// <summary>
        /// True If redelivery is used for the queue
        /// </summary>
        public bool UseRedelivery { get; }

        /// <inheritdoc />
        public HorseQueue Queue { get; }
        
        /// <inheritdoc />
        public IQueueMessageStore MessageStore => _messageStore;
        
        /// <inheritdoc />
        public IQueueMessageStore PriorityMessageStore => _priorityMessageStore;
        
        /// <inheritdoc />
        public IQueueDeliveryHandler DeliveryHandler => _deliveryHandler;
        
        /// <inheritdoc />
        public IQueueSynchronizer Synchronizer { get; }

        private readonly PersistentMessageDeliveryHandler _deliveryHandler;
        private readonly PersistentMessageStore _priorityMessageStore;
        private readonly PersistentMessageStore _messageStore;

        /// <summary>
        /// Creates new persistent queue manager
        /// </summary>
        public PersistentQueueManager(HorseQueue queue, DatabaseOptions databaseOptions, bool useRedelivery = false)
        {
            Queue = queue;
            UseRedelivery = useRedelivery;
            queue.Manager = this;

            _deliveryHandler = new PersistentMessageDeliveryHandler(this);

            DatabaseOptions prioDbOptions = databaseOptions.Clone();
            if (prioDbOptions.Filename.EndsWith(".tdb", StringComparison.InvariantCultureIgnoreCase))
                prioDbOptions.Filename = prioDbOptions.Filename.Substring(0, prioDbOptions.Filename.Length - 4) +
                                         "_priority" +
                                         ".tdb";
            else
                prioDbOptions.Filename += "_priority";

            _priorityMessageStore = new PersistentMessageStore(this, prioDbOptions);
            _messageStore = new PersistentMessageStore(this, databaseOptions);

            Synchronizer = new MemoryQueueSynchronizer(this);
        }

        public async Task Initialize()
        {
            if (Queue.Options.Acknowledge == QueueAckDecision.None && Queue.Options.CommitWhen == CommitWhen.AfterAcknowledge)
                    throw new NotSupportedException("Producer Ack option is AfterConsumerAckReceived but queue Acknowledge option is None. " +
                                                    "Messages are not deleted from disk with this configuration. " +
                                                    "Please change queue Acknowledge option or ProducerAckDecision option");

            await _priorityMessageStore.Initialize();
            await _messageStore.Initialize();

            Queue.OnDestroyed += DestoryAsync;

            string path = _messageStore.Database.File.Filename;
            List<KeyValuePair<string, int>> deliveries = null;
            if (UseRedelivery)
            {
                RedeliveryService = new RedeliveryService($"{path}.delivery");
                await RedeliveryService.Load();
                deliveries = RedeliveryService.GetDeliveries();
            }

            bool added = ConfigurationFactory.Manager.Add(Queue, _messageStore.Database.File.Filename);
            if (added)
                ConfigurationFactory.Manager.Save();
            
            await LoadMessages(_priorityMessageStore.Database, deliveries);
            await LoadMessages(_messageStore.Database, deliveries);
        }

        private async Task LoadMessages(Database database, List<KeyValuePair<string, int>> deliveries)
        {
            var dict = await database.List();
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

        private async void DestoryAsync(HorseQueue queue)
        {
            try
            {
                await Destroy();
            }
            catch
            {
            }
        }

        public async Task Destroy()
        {
            if (UseRedelivery)
            {
                await RedeliveryService.Close();
                RedeliveryService.Delete();
            }

            try
            {
                ConfigurationFactory.Manager.Remove(Queue);
                ConfigurationFactory.Manager.Save();

                await _priorityMessageStore.Destroy();
                await _messageStore.Destroy();
            }
            catch (Exception e)
            {
                if (ConfigurationFactory.Builder.ErrorAction != null)
                    ConfigurationFactory.Builder.ErrorAction(Queue, null, e);
            }
        }

        public Task OnExceptionThrown(string hint, QueueMessage message, Exception exception)
        {
            if (ConfigurationFactory.Builder.ErrorAction != null)
                ConfigurationFactory.Builder.ErrorAction(Queue, message, exception);

            return Task.CompletedTask;
        }

        public async Task OnMessageTimeout(QueueMessage message)
        {
            if (message.Message.HighPriority)
                await _priorityMessageStore.RemoveMessage(message);
            else
                await _messageStore.RemoveMessage(message);
        }

        public bool AddMessage(QueueMessage message)
        {
            if (message.Message.HighPriority)
                _priorityMessageStore.Put(message);
            else
                _messageStore.Put(message);

            return true;
        }

        public async Task<bool> RemoveMessage(QueueMessage message)
        {
            if (UseRedelivery)
                await RedeliveryService.Remove(message.Message.MessageId);

            bool deleted = message.Message.HighPriority
                ? await _priorityMessageStore.RemoveMessage(message)
                : await _messageStore.RemoveMessage(message);

            return deleted;
        }

        public async Task<bool> RemoveMessage(string messageId)
        {
            if (UseRedelivery)
                await RedeliveryService.Remove(messageId);

            bool deleted = await _messageStore.RemoveMessage(messageId);

            if (!deleted)
                deleted = await _priorityMessageStore.RemoveMessage(messageId);

            return deleted;
        }

        public Task<bool> SaveMessage(QueueMessage message)
        {
            if (message.Message.HighPriority)
            {
                return _priorityMessageStore.Database.Insert(message.Message);
            }

            return _messageStore.Database.Insert(message.Message);
        }

        public async Task<bool> ChangeMessagePriority(QueueMessage message, bool priority)
        {
            if (message.Message.HighPriority == priority)
                return false;

            if (priority)
            {
                await _messageStore.RemoveMessage(message);
                _priorityMessageStore.Put(message);
                
                if (message.IsSaved)
                    await _priorityMessageStore.RemoveMessage(message);
            }
            else
            {
                await _priorityMessageStore.RemoveMessage(message);
                _messageStore.Put(message);
                
                if (message.IsSaved)
                    await _messageStore.RemoveMessage(message);
            }

            return true;
        }
    }
}