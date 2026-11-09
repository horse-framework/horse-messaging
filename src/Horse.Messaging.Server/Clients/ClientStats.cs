namespace Horse.Messaging.Server.Clients;

/// <summary>
/// Client Statistics
/// </summary>
public class ClientStats
{
    private long _receivedDirectMessages;

    /// <summary>
    /// Total received messages.
    /// It includes only direct messages or responses of direct messages
    /// </summary>
    public long ReceivedDirectMessages
    {
        get => System.Threading.Interlocked.Read(ref _receivedDirectMessages);
        set => System.Threading.Interlocked.Exchange(ref _receivedDirectMessages, value);
    }

    /// <summary>
    /// Sent direct messages or responses count.
    /// Increased by the sender's own receive loop, which processes one message at a time.
    /// </summary>
    public long SentDirectMessages { get; set; }

    /// <summary>
    /// Many senders can send to the same client at the same time, so the received count is increased atomically
    /// </summary>
    internal void IncrementReceivedDirectMessages()
    {
        System.Threading.Interlocked.Increment(ref _receivedDirectMessages);
    }

    /// <summary>
    /// Message push count to all queues of the client
    /// </summary>
    public long QueuePushes { get; set; }

    /// <summary>
    /// Message publish count to all channel of the client
    /// </summary>
    public long ChannelPublishes { get; set; }

    /// <summary>
    /// Message publish count to all routers of the client
    /// </summary>
    public long RouterPublishes { get; set; }

    /// <summary>
    /// The count of how many times client sets a key
    /// </summary>
    public long CacheSets { get; set; }

    /// <summary>
    /// The count of how many times client gets a cache value
    /// </summary>
    public long CacheGets { get; set; }

    private long _droppedEvents;

    /// <summary>
    /// Event messages dropped because the client's event buffer was full
    /// </summary>
    public long DroppedEvents => System.Threading.Interlocked.Read(ref _droppedEvents);

    internal void IncrementDroppedEvents()
    {
        System.Threading.Interlocked.Increment(ref _droppedEvents);
    }
}
