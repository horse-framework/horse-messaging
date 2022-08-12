using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Data.Implementation
{
    /// <summary>
    /// Queue manager for persistent queues
    /// </summary>
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
        public IQueueDeliveryHandler DeliveryHandler { get; protected set; }

        /// <inheritdoc />
        public IQueueSynchronizer Synchronizer { get; }

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

            DeliveryHandler = new PersistentMessageDeliveryHandler(this);

            DatabaseOptions prioDbOptions = databaseOptions.Clone();
            if (prioDbOptions.Filename.EndsWith(".tdb", StringComparison.InvariantCultureIgnoreCase))
                prioDbOptions.Filename = prioDbOptions.Filename.Substring(0, prioDbOptions.Filename.Length - 4) +
                                         "_priority" +
                                         ".tdb";
            else
                prioDbOptions.Filename += "_priority";

            _priorityMessageStore = new PersistentMessageStore(this, prioDbOptions);
            _messageStore = new PersistentMessageStore(this, databaseOptions);

            Synchronizer = new DefaultQueueSynchronizer(this);
        }

        /// <summary>
        /// Initializes the manager, loads messages from disk and initializes redelivery
        /// </summary>
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

            await LoadMessages(_priorityMessageStore.Database, deliveries);
            await LoadMessages(_messageStore.Database, deliveries);

            DeliveryHandler.Tracker.Start();
            PriorityMessageStore.TimeoutTracker.Start();
            MessageStore.TimeoutTracker.Start();
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

        /// <inheritdoc />
        public async Task Destroy()
        {
            if (UseRedelivery)
            {
                await RedeliveryService.Close();
                RedeliveryService.Delete();
            }

            try
            {
                await _priorityMessageStore.Destroy();
                await _messageStore.Destroy();
            }
            catch (Exception e)
            {
                Queue.Rider.SendError("PersistentQueueDestroy", e, Queue.Name);
            }

            PriorityMessageStore.TimeoutTracker.Stop();
            MessageStore.TimeoutTracker.Stop();

            await DeliveryHandler.Tracker.Destroy();
            await PriorityMessageStore.Destroy();
            await MessageStore.Destroy();
        }

        /// <inheritdoc />
        public Task OnExceptionThrown(string hint, QueueMessage message, Exception exception)
        {
            Queue.Rider.SendError(hint, exception, $"MessageId: {message.Message.MessageId}");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task OnMessageTimeout(QueueMessage message)
        {
            if (UseRedelivery)
                await RedeliveryService.Remove(message.Message.MessageId);
        }

        /// <inheritdoc />
        public bool AddMessage(QueueMessage message)
        {
            if (message.Message.HighPriority)
                _priorityMessageStore.Put(message);
            else
                _messageStore.Put(message);

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMessage(QueueMessage message)
        {
            if (UseRedelivery)
                await RedeliveryService.Remove(message.Message.MessageId);

            bool deleted = message.Message.HighPriority
                ? await _priorityMessageStore.RemoveMessage(message)
                : await _messageStore.RemoveMessage(message);

            return deleted;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveMessage(string messageId)
        {
            if (UseRedelivery)
                await RedeliveryService.Remove(messageId);

            bool deleted = await _messageStore.RemoveMessage(messageId);

            if (!deleted)
                deleted = await _priorityMessageStore.RemoveMessage(messageId);

            return deleted;
        }

        /// <inheritdoc />
        public Task<bool> SaveMessage(QueueMessage message)
        {
            if (message.Message.HighPriority)
            {
                return _priorityMessageStore.Database.Insert(message.Message);
            }

            return _messageStore.Database.Insert(message.Message);
        }

        /// <inheritdoc />
        public Task<bool> ChangeMessagePriority(QueueMessage message, bool priority)
        {
            if (message.Message.HighPriority == priority)
                return Task.FromResult(false);

            message.Message.HighPriority = priority;

            if (priority)
            {
                _messageStore.RemoveFromOnlyMemory(message);
                _priorityMessageStore.Put(message);
            }
            else
            {
                _priorityMessageStore.RemoveFromOnlyMemory(message);
                _messageStore.Put(message);
            }

            return Task.FromResult(true);
        }
    }
}