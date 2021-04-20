namespace Horse.Messaging.Client.Events
{
    /// <summary>
    /// Horse Event Types
    /// </summary>
    internal enum HorseEventType
    {
        ClientConnect,
        ClientDisconnect,
        
        ConnectedToRemoteNode,
        DisconnectedFromRemoteNode,
        RemoteNodeConnect,
        RemoteNodeDisconnect,
        
        QueueCreate,
        QueueRemove,
        QueueStatusChange,
        QueueOptionsUpdate,
        QueueSubscription,
        QueueUnsubscription,
        MessagePushedToQueue,
        QueueMessageAck,
        QueueMessageNack,
        QueueMessageUnack,
        QueueMessageTimeout,
        
        RouterCreate,
        RouterRemove,
        RouterBindingAdd,
        RouterBindingRemov,
        MessagePublishedToRouter,
        
        DirectMessage,
        DirectMessageResponse,
        
        CacheGet,
        CacheSet,
        CacheRemove,
        CachePurge
    }
}