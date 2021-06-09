namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Horse Message types
    /// </summary>
    public enum MessageType : byte
    {
        /// <summary>
        /// Unknown message, may be peer to peer
        /// </summary>
        Other = 0x00,

        /// <summary>
        /// Connection close request
        /// </summary>
        Terminate = 0x08,

        /// <summary>
        /// Ping message from server
        /// </summary>
        Ping = 0x09,

        /// <summary>
        /// Pong message to server
        /// </summary>
        Pong = 0x0A,

        /// <summary>
        /// A message to directly server.
        /// Server should deal with it directly.
        /// </summary>
        Server = 0x10,

        /// <summary>
        /// A message to a queue
        /// </summary>
        QueueMessage = 0x11,

        /// <summary>
        /// Direct message, by Id, @type or @name
        /// </summary>
        DirectMessage = 0x12,

        /// <summary>
        /// A response message, point to a message received before.
        /// </summary>
        Response = 0x14,

        /// <summary>
        /// Used for requesting to pull messages from the queue
        /// </summary>
        QueuePullRequest = 0x15,

        /// <summary>
        /// Notifies events if it's from server to client.
        /// Subscribes or ubsubscribes events if it's from client to server. 
        /// </summary>
        Event = 0x16,

        /// <summary>
        /// Message is routed to a custom exchange in server
        /// </summary>
        Router = 0x17,
        
        /// <summary>
        /// Channel messages
        /// </summary>
        Channel = 0x18,
        
        /// <summary>
        /// Cache operation messages
        /// </summary>
        Cache = 0x19,
        
        /// <summary>
        /// Pipe messages
        /// </summary>
        Pipe = 0x1A,
        
        /// <summary>
        /// Transaction messages
        /// </summary>
        Transaction = 0x1B
    }
}