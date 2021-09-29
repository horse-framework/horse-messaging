namespace Horse.Messaging.Server.Cluster
{
    /// <summary>
    /// Queue message sync method
    /// </summary>
    public enum SyncMethod : byte
    {
        /// <summary>
        /// The message will be added to beginning of the queue
        /// </summary>
        AddFirst = 0,
        
        /// <summary>
        /// The message will be added after specified message
        /// </summary>
        AddAfter = 1,
        
        /// <summary>
        /// The message will be added to end of the queue
        /// </summary>
        AddLast = 2,
        
        /// <summary>
        /// Message adding operation is completed
        /// </summary>
        Completed = 3
    }

    /// <summary>
    /// Message sync method model
    /// </summary>
    public class MessageSyncMethod
    {
        /// <summary>
        /// Message sync method
        /// </summary>
        public SyncMethod Method { get; }
        
        /// <summary>
        /// If the message will be added after another message.
        /// Previous message id.
        /// </summary>
        public string PreviousMessageId { get; }

        /// <summary>
        /// Creates new message sync method
        /// </summary>
        public MessageSyncMethod(SyncMethod method, string previousMessageId)
        {
            Method = method;
            PreviousMessageId = previousMessageId;
        }
    }
}