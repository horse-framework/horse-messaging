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
    private PartitionEntry _orphanPartition;
    private Timer _autoDestroyTimer;
    private int _roundRobinIndex; // used for label-less routing when orphan is disabled

    /// <summary>All active partition entries (snapshot).</summary>
    public IEnumerable<PartitionEntry> Partitions => _partitions.Values;

    /// <summary>Orphan partition entry, or null if not yet created.</summary>
    public PartitionEntry OrphanPartition => _orphanPartition;

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
    /// Also subscribes client to orphan partition for fallback message delivery
    /// (only when EnableOrphanPartition = true).
    /// </summary>
    public async Task<PartitionEntry> SubscribeClient(MessagingClient client, string partitionLabel)
    {
        PartitionEntry entry;

        if (!string.IsNullOrEmpty(partitionLabel))
        {
            entry = await GetOrCreateLabelPartition(partitionLabel);
            SubscriptionResult result = await entry.Queue.AddClient(client);
            if (result == SubscriptionResult.Full)
                entry = await SubscribeToOrphan(client);
        }
        else
        {
            entry = null;
            foreach (PartitionEntry existing in _partitions.Values.Where(p => !p.IsOrphan))
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
                                    _partitions.Values.Count(p => !p.IsOrphan) >= _options.MaxPartitionCount;
                if (!limitReached)
                {
                    entry = await CreatePartition(null);
                    await entry.Queue.AddClient(client);
                }
                else
                    entry = await SubscribeToOrphan(client);
            }
        }

        // Subscribe to orphan so worker also receives fallback messages
        if (entry != null && !entry.IsOrphan && _options.EnableOrphanPartition)
        {
            HorseQueue orphanQueue = await GetOrCreateOrphanQueue();
            if (orphanQueue.FindClient(client.UniqueId) == null)
                await orphanQueue.AddClient(client);
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
    /// - Label present + active subscriber  → deliver directly to labeled partition.
    /// - Label present + NO subscriber      → store in the labeled partition queue
    ///   (creates it if it doesn't exist yet) so the message waits for the owner
    ///   worker to reconnect.  Never cross-delivered to another worker.
    /// - Label absent + orphan enabled      → orphan partition.
    /// - Label absent + orphan disabled     → round-robin across active partitions.
    /// </summary>
    public async Task<(HorseQueue target, PushResult result)> RouteMessage(QueueMessage message, MessagingClient sender)
    {
        string label = message.Message.FindHeader(HorseHeaders.PARTITION_LABEL);
        HorseQueue target;

        if (!string.IsNullOrEmpty(label))
        {
            // Always route to the labeled partition — whether or not a subscriber is
            // currently present.  The message is stored there and delivered as soon as
            // the owning worker reconnects.  This guarantees tenant isolation: a message
            // labeled "tenant-A" can never be consumed by a "tenant-B" worker.
            PartitionEntry entry = await GetOrCreateLabelPartition(label);
            target = entry.Queue;
            entry.LastMessageAt = DateTime.UtcNow;
        }
        else
        {
            // Label-less message: use orphan when enabled, otherwise route to
            // the least-loaded non-orphan partition (fewest stored messages).
            if (_options.EnableOrphanPartition)
                target = await GetOrCreateOrphanQueue();
            else
            {
                // Round-robin across non-orphan partitions that have active subscribers
                var available = _partitions.Values
                    .Where(p => !p.IsOrphan && p.Queue.Clients.Any())
                    .OrderBy(p => p.PartitionId)   // stable sort for determinism
                    .ToList();

                if (available.Count == 0)
                {
                    target = null;
                }
                else
                {
                    int idx = Math.Abs(Interlocked.Increment(ref _roundRobinIndex)) % available.Count;
                    target = available[idx].Queue;
                }
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

            // returnIfExists=true: if the queue was already restored from queues.json
            // (restart scenario), reuse it instead of throwing DuplicateNameException.
            HorseQueue partitionQueue = await _parentQueue.Rider.Queue.Create(
                queueName, partitionOptions, null, hideException: false, returnIfExists: true);

            partitionQueue.IsPartitionQueue = true;
            partitionQueue.PartitionMeta = new SubPartitionMeta
            {
                ParentQueueName = _parentQueue.Name,
                PartitionId     = partitionId,
                Label           = label,
                IsOrphan        = false
            };
            // Persist the SubPartition metadata to queues.json now that PartitionMeta is set
            partitionQueue.UpdateConfiguration(false);

            var entry = new PartitionEntry
            {
                PartitionId = partitionId,
                Label = label,
                IsOrphan = false,
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
    /// The HorseQueue already exists (loaded by QueueRider.Initialize); we just
    /// wire up the bookkeeping structures.
    /// </summary>
    public void ReAttach(HorseQueue partitionQueue, string partitionId, string label, bool isOrphan)
    {
        partitionQueue.IsPartitionQueue = true;
        partitionQueue.PartitionMeta = new SubPartitionMeta
        {
            ParentQueueName = _parentQueue.Name,
            PartitionId     = partitionId,
            Label           = label,
            IsOrphan        = isOrphan
        };

        var entry = new PartitionEntry
        {
            PartitionId = partitionId,
            Label       = label,
            IsOrphan    = isOrphan,
            Queue       = partitionQueue
        };

        if (!string.IsNullOrEmpty(label) && !isOrphan)
            _labelIndex[label] = entry;

        _partitions[partitionId] = entry;

        if (isOrphan)
        {
            _orphanPartition = entry;
            partitionQueue.OnDestroyed += _ => _orphanPartition = null;
        }
        else
        {
            partitionQueue.OnDestroyed += _ => OnPartitionQueueDestroyed(entry);
        }
    }

    private void OnPartitionQueueDestroyed(PartitionEntry entry)
    {
        _partitions.TryRemove(entry.PartitionId, out _);

        if (!string.IsNullOrEmpty(entry.Label))
            _labelIndex.TryRemove(entry.Label, out _);

        if (entry.IsOrphan)
            _orphanPartition = null;

        foreach (IPartitionEventHandler handler in _parentQueue.Rider.Queue.PartitionEventHandlers.All())
            _ = handler.OnPartitionDestroyed(_parentQueue, entry.PartitionId);
    }

    #endregion

    #region Orphan Partition

    /// <summary>
    /// Returns the orphan (fallback) partition queue, creating it if necessary.
    /// The orphan partition has a fixed suffix "-Partition-Orphan" for predictability.
    /// Uses returnIfExists=true so restart recovery (where the queue was already
    /// loaded from queues.json) works without a DuplicateNameException.
    /// </summary>
    public async Task<HorseQueue> GetOrCreateOrphanQueue()
    {
        if (_orphanPartition != null)
            return _orphanPartition.Queue;

        await _createLock.WaitAsync();
        try
        {
            if (_orphanPartition != null)
                return _orphanPartition.Queue;

            string queueName = $"{_parentQueue.Name}-Partition-Orphan";

            QueueOptions orphanOptions = QueueOptions.CloneFrom(_parentQueue.Options);
            orphanOptions.ClientLimit = 0; // Unlimited subscribers on orphan
            orphanOptions.AutoQueueCreation = false;
            orphanOptions.Partition = null;

            // returnIfExists=true: reuse already-restored queue on restart
            HorseQueue orphanQueue = await _parentQueue.Rider.Queue.Create(
                queueName, orphanOptions, null, hideException: false, returnIfExists: true);

            orphanQueue.IsPartitionQueue = true;
            orphanQueue.PartitionMeta = new SubPartitionMeta
            {
                ParentQueueName = _parentQueue.Name,
                PartitionId     = "Orphan",
                Label           = null,
                IsOrphan        = true
            };
            // Persist SubPartition metadata to queues.json
            orphanQueue.UpdateConfiguration(false);

            _orphanPartition = new PartitionEntry
            {
                PartitionId = "Orphan",
                Label = null,
                IsOrphan = true,
                Queue = orphanQueue
            };

            _partitions["Orphan"] = _orphanPartition;
            orphanQueue.OnDestroyed += _ => _orphanPartition = null;
            FirePartitionCreatedEvent(_orphanPartition);

            foreach (IPartitionEventHandler handler in _parentQueue.Rider.Queue.PartitionEventHandlers.All())
                _ = handler.OnPartitionCreated(_parentQueue, _orphanPartition);

            return orphanQueue;
        }
        finally
        {
            _createLock.Release();
        }
    }

    #endregion

    #region Helpers

    private async Task<PartitionEntry> GetOrCreateLabelPartition(string label)
    {
        if (_labelIndex.TryGetValue(label, out PartitionEntry entry))
            return entry;
        return await CreatePartition(label);
    }


    private async Task<PartitionEntry> SubscribeToOrphan(MessagingClient client)
    {
        if (!_options.EnableOrphanPartition)
            return null;

        HorseQueue orphanQueue = await GetOrCreateOrphanQueue();
        await orphanQueue.AddClient(client);
        return _orphanPartition;
    }

    private string GetPartitionIdFromQueue(HorseQueue queue)
    {
        foreach (PartitionEntry entry in _partitions.Values)
            if (entry.Queue == queue)
                return entry.PartitionId;
        return string.Empty;
    }

    private void FirePartitionCreatedEvent(PartitionEntry entry)
    {
        KeyValuePair<string, string>[] parameters =
        {
            new(HorseHeaders.QUEUE_NAME, _parentQueue.Name),
            new(HorseHeaders.PARTITION_ID, entry.PartitionId),
            new("Partition-Queue", entry.Queue.Name),
            new("Partition-Is-Orphan", entry.IsOrphan ? "true" : "false"),
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

        foreach (PartitionEntry entry in _partitions.Values.Where(e => !e.IsOrphan).ToList())
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
                IsOrphan      = entry.IsOrphan,
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
