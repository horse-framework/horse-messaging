using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Manages partitions for a partitioned parent queue.
/// Attached to HorseQueue.PartitionManager when PartitionOptions.Enabled = true.
/// </summary>
public class PartitionManager
{
    private readonly HorseQueue _parentQueue;
    private readonly PartitionOptions _options;
    private readonly SemaphoreSlim _createLock = new(1, 1);
    private readonly ConcurrentDictionary<string, PartitionEntry> _partitions = new();
    private readonly ConcurrentDictionary<string, PartitionEntry> _labelIndex = new(StringComparer.OrdinalIgnoreCase);
    private Timer _autoDestroyTimer;
    private int _roundRobinIndex;

    /// <summary>
    /// Pool of workers waiting to be auto-assigned to partitions.
    /// Only used when <see cref="PartitionOptions.AutoAssignWorkers"/> is true.
    /// Workers subscribe without a label and wait here until a partition needs a consumer.
    /// </summary>
    private readonly ConcurrentQueue<MessagingClient> _availableWorkers = new();

    /// <summary>
    /// Tracks how many partitions each worker is currently assigned to.
    /// Key = MessagingClient.UniqueId, Value = current assignment count.
    /// Only used when <see cref="PartitionOptions.AutoAssignWorkers"/> is true.
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _workerAssignmentCount = new();

    /// <summary>All active partition entries (snapshot).</summary>
    public IEnumerable<PartitionEntry> Partitions => _partitions.Values;

    public PartitionManager(HorseQueue parentQueue, PartitionOptions options)
    {
        _parentQueue = parentQueue;
        _options = options;
        if (options.AutoDestroy != PartitionAutoDestroy.Disabled && options.AutoDestroyIdleSeconds > 0)
        {
            var interval = TimeSpan.FromSeconds(options.AutoDestroyIdleSeconds);
            _autoDestroyTimer = new Timer(CheckAutoDestroy, null, interval, interval);
        }
    }

    #region Subscribe Flow

    /// <summary>
    /// Called when a client subscribes to the parent queue.
    /// Finds or creates a suitable partition and subscribes the client to it.
    /// <para>
    /// When <see cref="PartitionOptions.AutoAssignWorkers"/> is true and no label is provided,
    /// the worker is added to the available pool and will be auto-assigned when a labeled
    /// partition needs a consumer. In this case the method returns a synthetic entry with
    /// <see cref="PartitionEntry.PartitionId"/> set to "pool" to signal successful pool registration.
    /// </para>
    /// Returns null when no partition slot is available (label partition full or max partitions reached).
    /// </summary>
    public async Task<PartitionEntry> SubscribeClient(MessagingClient client, string partitionLabel)
    {
        PartitionEntry entry;

        if (!string.IsNullOrEmpty(partitionLabel))
        {
            entry = await GetOrCreateLabelPartition(partitionLabel);
            SubscriptionResult result = await entry.Queue.AddClient(client);
            if (result == SubscriptionResult.Full)
                return null;
        }
        else if (_options.AutoAssignWorkers)
        {
            // Try to assign to an existing partition that needs a consumer first
            entry = await TryAssignToExistingPartition(client);

            if (entry != null)
            {
                // Worker was assigned to existing partition(s).
                // If it still has capacity, keep it in the pool for future on-demand assignments.
                int currentCount = _workerAssignmentCount.GetValueOrDefault(client.UniqueId, 0);
                int max = _options.MaxPartitionsPerWorker;
                if (max == 0 || currentCount < max)
                    _availableWorkers.Enqueue(client);
            }
            else
            {
                // No partition needs a consumer right now; add to pool
                _availableWorkers.Enqueue(client);

                // Return a synthetic entry to signal "you're in the pool, we'll assign you later"
                entry = new PartitionEntry
                {
                    PartitionId = "pool",
                    Label = null,
                    Queue = null
                };
            }
        }
        else
        {
            entry = null;
            foreach (PartitionEntry existing in _partitions.Values)
            {
                SubscriptionResult r = await existing.Queue.AddClient(client);
                if (r == SubscriptionResult.Success)
                {
                    entry = existing;
                    break;
                }
            }

            if (entry == null)
            {
                bool limitReached = _options.MaxPartitionCount > 0 &&
                                    _partitions.Count >= _options.MaxPartitionCount;
                if (!limitReached)
                {
                    entry = await CreatePartition(null);
                    await entry.Queue.AddClient(client);
                }
            }
        }

        return entry;
    }

    #endregion

    #region Push / Route Flow

    /// <summary>
    /// Routes an incoming message to the appropriate partition queue.
    /// Called by the parent queue's Push intercept.
    ///
    /// Key behaviours:
    /// - Label present  → deliver directly to labeled partition (creates if needed).
    ///                     When AutoAssignWorkers is enabled and the partition has no subscriber,
    ///                     a worker is pulled from the pool and assigned automatically.
    /// - Label absent   → round-robin across active partitions.
    /// </summary>
    public async Task<(HorseQueue target, PushResult result)> RouteMessage(QueueMessage message, MessagingClient sender)
    {
        string label = message.Message.FindHeader(HorseHeaders.PARTITION_LABEL);
        HorseQueue target;

        if (!string.IsNullOrEmpty(label))
        {
            PartitionEntry entry = await GetOrCreateLabelPartition(label);
            target = entry.Queue;
            entry.LastMessageAt = DateTime.UtcNow;

            // Auto-assign: if partition has no consumer, pull one from the worker pool
            if (_options.AutoAssignWorkers && !target.Clients.Any())
                await TryAssignPooledWorker(entry);
        }
        else
        {
            // Round-robin across partitions that have active subscribers
            var available = _partitions.Values
                .Where(p => p.Queue.Clients.Any())
                .OrderBy(p => p.PartitionId)
                .ToList();

            if (available.Count == 0)
                target = null;
            else
            {
                int idx = Math.Abs(Interlocked.Increment(ref _roundRobinIndex)) % available.Count;
                target = available[idx].Queue;
            }
        }

        if (target == null)
            return (null, PushResult.NoConsumers);

        // Strip routing header before forwarding to consumers
        message.Message.RemoveHeaders(HorseHeaders.PARTITION_LABEL);

        // Stamp which partition the message is going to
        string partitionId = GetPartitionIdFromQueue(target);
        if (!string.IsNullOrEmpty(partitionId))
            message.Message.AddHeader(HorseHeaders.PARTITION_ID, partitionId);

        PushResult pushResult = await target.Push(message, sender);
        return (target, pushResult);
    }

    #endregion

    #region Partition Lifecycle

    /// <summary>
    /// Creates a new partition queue, registers it, and fires QueuePartitionCreated event.
    /// </summary>
    /// <param name="label">Routing label, or null for label-less partitions.</param>
    /// <param name="preKnownQueueName">
    /// When non-null the queue is created (or reused) under this exact name instead of
    /// generating a new GUID-based name.  Used during restart recovery so that existing
    /// .hdb files are picked up automatically.
    /// </param>
    /// <param name="preKnownPartitionId">
    /// When non-null this id is used instead of generating a new one.
    /// </param>
    public async Task<PartitionEntry> CreatePartition(string label,
        string preKnownQueueName = null,
        string preKnownPartitionId = null)
    {
        await _createLock.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(label) && _labelIndex.TryGetValue(label, out PartitionEntry existing))
                return existing;

            string partitionId;
            string queueName;

            if (!string.IsNullOrEmpty(preKnownPartitionId) && !string.IsNullOrEmpty(preKnownQueueName))
            {
                partitionId = preKnownPartitionId;
                queueName   = preKnownQueueName;
            }
            else
            {
                partitionId = PartitionIdGenerator.Generate();
                while (_partitions.ContainsKey(partitionId))
                    partitionId = PartitionIdGenerator.Generate();
                queueName = $"{_parentQueue.Name}-Partition-{partitionId}";
            }

            QueueOptions partitionOptions = QueueOptions.CloneFrom(_parentQueue.Options);
            partitionOptions.ClientLimit = _options.SubscribersPerPartition;
            partitionOptions.AutoQueueCreation = false;
            partitionOptions.Partition = null; // Prevent recursive partitioning

            HorseQueue partitionQueue = await _parentQueue.Rider.Queue.Create(
                queueName, partitionOptions, null, hideException: false, returnIfExists: true);

            partitionQueue.IsPartitionQueue = true;
            partitionQueue.PartitionMeta = new SubPartitionMeta
            {
                ParentQueueName = _parentQueue.Name,
                PartitionId     = partitionId,
                Label           = label
            };
            partitionQueue.UpdateConfiguration(false);

            var entry = new PartitionEntry
            {
                PartitionId = partitionId,
                Label = label,
                Queue = partitionQueue
            };

            if (!string.IsNullOrEmpty(label))
                _labelIndex[label] = entry;

            _partitions[partitionId] = entry;
            partitionQueue.OnDestroyed += _ => OnPartitionQueueDestroyed(entry);
            FirePartitionCreatedEvent(entry);

            foreach (IPartitionEventHandler handler in _parentQueue.Rider.Queue.PartitionEventHandlers.All())
                _ = handler.OnPartitionCreated(_parentQueue, entry);

            return entry;
        }
        finally
        {
            _createLock.Release();
        }
    }

    /// <summary>
    /// Called on server restart to re-attach a partition sub-queue that was
    /// restored from queues.json back into this PartitionManager.
    /// </summary>
    public void ReAttach(HorseQueue partitionQueue, string partitionId, string label)
    {
        partitionQueue.IsPartitionQueue = true;
        partitionQueue.PartitionMeta = new SubPartitionMeta
        {
            ParentQueueName = _parentQueue.Name,
            PartitionId     = partitionId,
            Label           = label
        };

        var entry = new PartitionEntry
        {
            PartitionId = partitionId,
            Label       = label,
            Queue       = partitionQueue
        };

        if (!string.IsNullOrEmpty(label))
            _labelIndex[label] = entry;

        _partitions[partitionId] = entry;
        partitionQueue.OnDestroyed += _ => OnPartitionQueueDestroyed(entry);
    }

    private void OnPartitionQueueDestroyed(PartitionEntry entry)
    {
        // When AutoAssignWorkers is enabled, decrement assignment counts and
        // ensure workers with remaining capacity are back in the pool
        if (_options.AutoAssignWorkers)
        {
            foreach (QueueClient qc in entry.Queue.Clients)
            {
                if (!qc.Client.IsConnected)
                {
                    _workerAssignmentCount.TryRemove(qc.Client.UniqueId, out _);
                    continue;
                }

                int newCount = _workerAssignmentCount.AddOrUpdate(
                    qc.Client.UniqueId, 0, (_, c) => Math.Max(0, c - 1));

                // If worker is not already in the pool and has capacity, add it back
                int max = _options.MaxPartitionsPerWorker;
                bool hasCapacity = max == 0 || newCount < max;
                bool alreadyInPool = _availableWorkers.Any(w => w.UniqueId == qc.Client.UniqueId);

                if (hasCapacity && !alreadyInPool)
                    _availableWorkers.Enqueue(qc.Client);

                if (newCount == 0)
                    _workerAssignmentCount.TryRemove(qc.Client.UniqueId, out _);
            }
        }

        _partitions.TryRemove(entry.PartitionId, out _);

        if (!string.IsNullOrEmpty(entry.Label))
            _labelIndex.TryRemove(entry.Label, out _);

        foreach (IPartitionEventHandler handler in _parentQueue.Rider.Queue.PartitionEventHandlers.All())
            _ = handler.OnPartitionDestroyed(_parentQueue, entry.PartitionId);
    }

    #endregion

    #region Helpers

    private async Task<PartitionEntry> GetOrCreateLabelPartition(string label)
    {
        if (_labelIndex.TryGetValue(label, out PartitionEntry entry))
            return entry;
        return await CreatePartition(label);
    }

    private string GetPartitionIdFromQueue(HorseQueue queue)
    {
        foreach (PartitionEntry entry in _partitions.Values)
            if (entry.Queue == queue)
                return entry.PartitionId;
        return string.Empty;
    }

    /// <summary>
    /// Attempts to find a connected worker from the available pool and assign it to the given partition.
    /// Respects <see cref="PartitionOptions.MaxPartitionsPerWorker"/>: if the limit is not yet reached
    /// after assignment, the worker stays in the pool for future assignments.
    /// Skips disconnected workers (lazy pool cleanup).
    /// </summary>
    private async Task TryAssignPooledWorker(PartitionEntry entry)
    {
        int poolSize = _availableWorkers.Count;
        for (int i = 0; i < poolSize; i++)
        {
            if (!_availableWorkers.TryDequeue(out MessagingClient worker))
                break;

            // Lazy cleanup: skip disconnected workers
            if (!worker.IsConnected)
            {
                _workerAssignmentCount.TryRemove(worker.UniqueId, out _);
                continue;
            }

            // Check if this worker has capacity for more partitions
            int currentCount = _workerAssignmentCount.GetValueOrDefault(worker.UniqueId, 0);
            int max = _options.MaxPartitionsPerWorker;
            if (max > 0 && currentCount >= max)
            {
                // Worker is at capacity — put back and try next
                _availableWorkers.Enqueue(worker);
                continue;
            }

            SubscriptionResult result = await entry.Queue.AddClient(worker);
            if (result == SubscriptionResult.Success)
            {
                int newCount = _workerAssignmentCount.AddOrUpdate(worker.UniqueId, 1, (_, c) => c + 1);

                // If worker still has capacity, keep it in the pool
                if (max == 0 || newCount < max)
                    _availableWorkers.Enqueue(worker);

                _ = entry.Queue.Trigger();
                return;
            }

            // Partition already full — put the worker back and stop trying
            if (result == SubscriptionResult.Full)
            {
                _availableWorkers.Enqueue(worker);
                return;
            }

            // Other failure — put worker back
            _availableWorkers.Enqueue(worker);
        }
    }

    /// <summary>
    /// When a new worker arrives with AutoAssignWorkers, try to place it into an existing
    /// partition that has room (fewer subscribers than SubscribersPerPartition).
    /// May assign to multiple partitions up to <see cref="PartitionOptions.MaxPartitionsPerWorker"/>.
    /// Returns the first assigned entry, or null if no partition needs a consumer.
    /// </summary>
    private async Task<PartitionEntry> TryAssignToExistingPartition(MessagingClient client)
    {
        PartitionEntry firstAssigned = null;
        int assignedCount = 0;
        int max = _options.MaxPartitionsPerWorker;

        foreach (PartitionEntry entry in _partitions.Values)
        {
            if (max > 0 && assignedCount >= max)
                break;

            if (entry.Queue.Clients.Count() < _options.SubscribersPerPartition)
            {
                SubscriptionResult result = await entry.Queue.AddClient(client);
                if (result == SubscriptionResult.Success)
                {
                    _ = entry.Queue.Trigger();
                    firstAssigned ??= entry;
                    assignedCount++;
                }
            }
        }

        if (assignedCount > 0)
            _workerAssignmentCount.AddOrUpdate(client.UniqueId, assignedCount, (_, c) => c + assignedCount);

        return firstAssigned;
    }

    private void FirePartitionCreatedEvent(PartitionEntry entry)
    {
        KeyValuePair<string, string>[] parameters =
        {
            new(HorseHeaders.QUEUE_NAME, _parentQueue.Name),
            new(HorseHeaders.PARTITION_ID, entry.PartitionId),
            new("Partition-Queue", entry.Queue.Name),
            new(HorseHeaders.PARTITION_LABEL, entry.Label ?? string.Empty)
        };
        _parentQueue.Rider.Queue.PartitionCreatedEvent?.Trigger(parameters);
    }

    #endregion

    #region AutoDestroy

    private async void CheckAutoDestroy(object state)
    {
        if (_options.AutoDestroy == PartitionAutoDestroy.Disabled)
            return;

        foreach (PartitionEntry entry in _partitions.Values.ToList())
        {
            bool shouldDestroy = _options.AutoDestroy switch
            {
                PartitionAutoDestroy.NoConsumers => !entry.Queue.Clients.Any(),
                PartitionAutoDestroy.NoMessages  => entry.Queue.IsEmpty,
                PartitionAutoDestroy.Empty       => !entry.Queue.Clients.Any() && entry.Queue.IsEmpty,
                _                                => false
            };

            if (shouldDestroy)
                await _parentQueue.Rider.Queue.Remove(entry.Queue);
        }
    }

    #endregion

    #region Metrics

    /// <summary>Returns a point-in-time snapshot of per-partition metrics.</summary>
    public IEnumerable<PartitionMetricSnapshot> GetMetrics()
    {
        foreach (PartitionEntry entry in _partitions.Values)
        {
            int msgCount = entry.Queue.Manager != null
                ? entry.Queue.Manager.MessageStore.Count() + entry.Queue.Manager.PriorityMessageStore.Count()
                : 0;

            yield return new PartitionMetricSnapshot
            {
                PartitionId   = entry.PartitionId,
                Label         = entry.Label,
                MessageCount  = msgCount,
                ConsumerCount = entry.Queue.Clients.Count(),
                QueueName     = entry.Queue.Name,
                CreatedAt     = entry.CreatedAt,
                LastMessageAt = entry.LastMessageAt
            };
        }
    }

    #endregion

    /// <summary>Disposes the auto-destroy timer.</summary>
    public void Dispose()
    {
        _autoDestroyTimer?.Dispose();
        _autoDestroyTimer = null;
    }
}
