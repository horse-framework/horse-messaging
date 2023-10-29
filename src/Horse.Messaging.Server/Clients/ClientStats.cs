namespace Horse.Messaging.Server.Clients;

/// <summary>
/// Client Statistics
/// </summary>
public class ClientStats
{
    /// <summary>
    /// Total received messages.
    /// It includes only direct messages or responses of direct messages
    /// </summary>
    public long ReceivedDirectMessages { get; set; }

    /// <summary>
    /// Sent direct messages or responses count
    /// </summary>
    public long SentDirectMessages { get; set; }

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
}