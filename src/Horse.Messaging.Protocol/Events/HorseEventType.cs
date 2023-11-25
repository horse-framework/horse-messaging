namespace Horse.Messaging.Protocol.Events;

/// <summary>
/// Horse Event Types
/// </summary>
public enum HorseEventType : short
{
    /// <summary>
    /// Triggered when a client is connected to the server
    /// </summary>
    ClientConnect = 101,

    /// <summary>
    /// Triggered when a client is disconnected from the server
    /// </summary>
    ClientDisconnect = 102,

    /// <summary>
    /// Triggered when the server connects to remote node
    /// </summary>
    ConnectedToRemoteNode = 111,

    /// <summary>
    /// Triggered when the server disconnects from remote node
    /// </summary>
    DisconnectedFromRemoteNode = 112,

    /// <summary>
    /// Triggered when a remote node is connected to the server
    /// </summary>
    RemoteNodeConnect = 113,

    /// <summary>
    /// Triggered when a remote node is disconnected from the server
    /// </summary>
    RemoteNodeDisconnect = 114,

    /// <summary>
    /// Triggered when new queue is created
    /// </summary>
    QueueCreate = 201,

    /// <summary>
    /// Triggered when a queue is removed
    /// </summary>
    QueueRemove = 202,

    /// <summary>
    /// Triggered when a queue status is changed
    /// </summary>
    QueueStatusChange = 203,

    /// <summary>
    /// Triggered when a client is subscribed to a queue
    /// </summary>
    QueueSubscription = 204,

    /// <summary>
    /// Triggered when a client is unsubscribed from a queue
    /// </summary>
    QueueUnsubscription = 205,

    /// <summary>
    /// Triggered when a new message is pushed to a queue
    /// </summary>
    QueuePush = 206,

    /// <summary>
    /// Triggered when a queue message is acknowledged by it's consumer
    /// </summary>
    QueueMessageAck = 207,

    /// <summary>
    /// Triggered when a queue message is negatice acknowledged by it's consumer
    /// </summary>
    QueueMessageNack = 208,

    /// <summary>
    /// Triggered when a queue message acknowledge is timed out
    /// </summary>
    QueueMessageUnack = 209,

    /// <summary>
    /// Triggered when a message is timed out and dequeued from a queue
    /// </summary>
    QueueMessageTimeout = 210,

    /// <summary>
    /// Triggered when new router is created
    /// </summary>
    RouterCreate = 301,

    /// <summary>
    /// Triggered when a router is removed
    /// </summary>
    RouterRemove = 302,

    /// <summary>
    /// Triggered when a router binding is created
    /// </summary>
    RouterBindingAdd = 303,

    /// <summary>
    /// Triggered when a router binding is removed
    /// </summary>
    RouterBindingRemove = 304,

    /// <summary>
    /// Triggered when a message is published to a router
    /// </summary>
    RouterPublish = 305,

    /// <summary>
    /// Triggered when a direct message is received
    /// </summary>
    DirectMessage = 401,

    /// <summary>
    /// Triggered when a direct message is respond
    /// </summary>
    DirectMessageResponse = 402,

    /// <summary>
    /// Triggered when a client requested to get a cache
    /// </summary>
    CacheGet = 501,

    /// <summary>
    /// Triggered when a client sets a cache
    /// </summary>
    CacheSet = 502,

    /// <summary>
    /// Triggered when a client is removed a cache
    /// </summary>
    CacheRemove = 503,

    /// <summary>
    /// Triggered when a client is purged all cache values
    /// </summary>
    CachePurge = 504,

    /// <summary>
    /// Triggered when a channel is created
    /// </summary>
    ChannelCreate = 601,
        
    /// <summary>
    /// Triggered when a channel is removed
    /// </summary>
    ChannelRemove = 602,
        
    /// <summary>
    /// Triggered when a client is subscribed to a channel
    /// </summary>
    ChannelSubscribe = 603,
        
    /// <summary>
    /// Triggered when a client is unsubscribed from a channel
    /// </summary>
    ChannelUnsubscribe = 604,
        
    /// <summary>
    /// Triggered when a new message is published to a channel
    /// </summary>
    ChannelPublish = 605
}