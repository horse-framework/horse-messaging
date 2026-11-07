using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Store;
using Horse.Messaging.Server.Queues.Sync;

namespace Horse.Messaging.Data.Implementation;

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
        if (prioDbOptions.Filename.EndsWith(".hdb", StringComparison.OrdinalIgnoreCase))
            prioDbOptions.Filename = prioDbOptions.Filename.Substring(0, prioDbOptions.Filename.Length - 4) +
                                     "_prio" +
                                     ".hdb";
        else
            prioDbOptions.Filename += "_prio";

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

        List<HorseMessage> prioMessages = await _priorityMessageStore.Initialize();
        List<HorseMessage> messages = await _messageStore.Initialize();

        // Taken from both files before either is changed below.
        HashSet<string> prioOrphanDeletes = _priorityMessageStore.Database.TakeOrphanDeletes();
        HashSet<string> orphanDeletes = _messageStore.Database.TakeOrphanDeletes();
        await ApplyOrphanDeletes(messages, _messageStore, prioOrphanDeletes);
        await ApplyOrphanDeletes(prioMessages, _priorityMessageStore, orphanDeletes);

        Queue.OnDestroyed += DestoryAsync;

        string path = _messageStore.Database.File.Filename;
        List<KeyValuePair<string, int>> deliveries = null;
        if (UseRedelivery)
        {
            RedeliveryService = new RedeliveryService($"{path}.delivery");
            await RedeliveryService.Load();
            deliveries = RedeliveryService.GetDeliveries();
        }

        LoadMessages(prioMessages, deliveries, true);
        LoadMessages(messages, deliveries, false);

        DeliveryHandler.Tracker.Start();
        PriorityMessageStore.TimeoutTracker.Start();
        MessageStore.TimeoutTracker.Start();
    }

    /// <summary>
    /// Deletes the messages of one file that older versions deleted in the other file.
    /// Their priority changed after they were saved, so the delete went to the wrong file and
    /// the message came back after restart. Only deletes that have no insert in their own file
    /// are taken, so a message deleted and saved again in the same file is not affected.
    /// </summary>
    private static async Task ApplyOrphanDeletes(List<HorseMessage> messages, PersistentMessageStore store, HashSet<string> deletedIds)
    {
        if (deletedIds == null || deletedIds.Count == 0 || messages.Count == 0)
            return;

        HashSet<string> deleted = null;
        messages.RemoveAll(x =>
        {
            if (x.MessageId == null || !deletedIds.Contains(x.MessageId))
                return false;

            (deleted ??= new HashSet<string>(StringComparer.Ordinal)).Add(x.MessageId);
            return true;
        });

        if (deleted == null)
            return;

        foreach (string id in deleted)
            await store.Database.Delete(id);
    }

    private void LoadMessages(List<HorseMessage> messages, List<KeyValuePair<string, int>> deliveries, bool priorityFile)
    {
        if (messages.Count == 0)
            return;

        MessageTimeoutStrategy timeout = Queue.Options.MessageTimeout;
        bool resetDeadline =
            timeout != null &&
            timeout.Policy != MessageTimeoutPolicy.NoTimeout &&
            timeout.MessageDuration > 0;

        QueueFiller filler = new QueueFiller(Queue);
        PushResult result = filler.FillMessage(messages,
            true,
            qm =>
            {
                qm.SavedToPriorityStore = priorityFile;

                if (resetDeadline)
                    qm.Deadline = DateTime.UtcNow.AddSeconds(timeout.MessageDuration);

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

        PriorityMessageStore.TimeoutTracker.Stop();
        MessageStore.TimeoutTracker.Stop();

        await DeliveryHandler.Tracker.Destroy();

        try
        {
            await _priorityMessageStore.Destroy();
            await _messageStore.Destroy();
        }
        catch (Exception e)
        {
            Queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.QueuePersistentDestroy, $"Persistent Queue Destroy: {Queue.Name}", e);
        }
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

        // The store is chosen by the current priority (where the message is in memory); the record
        // is deleted from the file it was saved to (see PersistentMessageStore.RecordDatabase).
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

        // The regular store's removal used to report success for any id, so a priority message
        // was never tried and stayed in memory and on disk. Replicas remove messages this way.
        PersistentMessageStore store = _priorityMessageStore;
        QueueMessage message = store.Find(messageId);
        if (message == null)
        {
            store = _messageStore;
            message = store.Find(messageId);
        }

        if (message != null)
            return await store.RemoveMessage(message);

        // Not in memory (in flight, or already removed). Its record can be in either file.
        await _messageStore.Database.Delete(messageId);
        await _priorityMessageStore.Database.Delete(messageId);
        return false;
    }

    /// <summary>
    /// Returns the store whose database file holds the record of the message
    /// </summary>
    internal PersistentMessageStore GetRecordStore(QueueMessage message)
    {
        bool priorityFile = message.SavedToPriorityStore ?? message.Message.HighPriority;
        return priorityFile ? _priorityMessageStore : _messageStore;
    }

    /// <inheritdoc />
    public Task<bool> SaveMessage(QueueMessage message)
    {
        // Recorded before the write: the priority may change later, the file does not.
        bool priority = message.Message.HighPriority;
        message.SavedToPriorityStore = priority;

        return priority
            ? _priorityMessageStore.Database.Insert(message.Message)
            : _messageStore.Database.Insert(message.Message);
    }

    /// <inheritdoc />
    public Task<bool> ChangeMessagePriority(QueueMessage message, bool priority)
    {
        if (message.Message.HighPriority == priority)
            return Task.FromResult(false);

        bool oldValue = message.Message.HighPriority;
        message.Message.HighPriority = priority;

        try
        {
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
        catch
        {
            message.Message.HighPriority = oldValue;
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public async Task FlushToDisk()
    {
        await _priorityMessageStore.Database.File.FlushToDisk();
        await _messageStore.Database.File.FlushToDisk();

        if (UseRedelivery && RedeliveryService != null)
            await RedeliveryService.Flush();
    }

    /// <inheritdoc />
    public async Task Flush()
    {
        PriorityMessageStore.TimeoutTracker.Stop();
        MessageStore.TimeoutTracker.Stop();
        await DeliveryHandler.Tracker.Destroy();

        if (UseRedelivery && RedeliveryService != null)
            await RedeliveryService.Close();

        await _priorityMessageStore.CloseAsync();
        await _messageStore.CloseAsync();
    }
}
